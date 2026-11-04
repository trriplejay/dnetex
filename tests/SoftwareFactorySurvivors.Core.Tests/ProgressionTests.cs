using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ProgressionTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);
    private const float Frame = 1f / 60f;

    [Fact]
    public void Experience_needed_per_level_grows_faster_than_linearly()
    {
        int[] expected = [2, 5, 8, 11, 15];
        Assert.Equal(expected, Enumerable.Range(1, 5).Select(Leveling.Required));

        for (var level = 1; level < 30; level++)
        {
            Assert.True(Leveling.Required(level + 1) >= Leveling.Required(level),
                $"Level {level + 1} cost decreased from level {level}");
        }
    }

    [Fact]
    public void Experience_carries_over_and_can_gain_several_levels_at_once()
    {
        var leveling = new Leveling();

        Assert.Equal(0, leveling.Add(1));
        Assert.Equal(1, leveling.Level);

        Assert.Equal(2, leveling.Add(2 + 5 - 1 + 3));
        Assert.Equal(3, leveling.Level);
        Assert.Equal(3, leveling.Experience);
        Assert.Equal(8, leveling.ExperienceToNextLevel);
    }

    [Fact]
    public void Each_owned_weapon_fires_on_its_own_timer_and_an_upgrade_leaves_the_others_unchanged()
    {
        // Laser at 1.0/s and Lightning at 1.2/s, each on its own timer: 10 s gives 10 volleys and 12 strikes.
        var laser = new OwnedWeapon(WeaponKind.Laser);
        var lightning = new OwnedWeapon(WeaponKind.Lightning);
        var (volleys, strikes) = (0, 0);
        for (var i = 0; i < 600; i++)
        {
            volleys += laser.Fire(Frame, Vector2.Zero, 0f).Count;
            strikes += lightning.Fire(Frame, Vector2.Zero, 0f).Count;
        }
        Assert.InRange(volleys, 9, 10);
        Assert.InRange(strikes, 11, 12);

        // In a session, laser upgrades change only the laser: the lightning keeps every stat and its timer progress.
        var session = TestEnemies.QuietSession(Center);
        var owned = session.Stats.AddWeapon(WeaponKind.Lightning);
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        var before = WeaponSnapshot(owned);
        Assert.Equal(0.6d, before.Progress, precision: 5);

        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserAttackSpeed), session.Stats);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage), session.Stats);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserProjectile), session.Stats);

        Assert.Equal(before, WeaponSnapshot(owned));
        var upgraded = session.Stats.Laser();
        Assert.Equal(1.25f, upgraded.AttacksPerSecond);
        Assert.Equal(2f, upgraded.Damage);
        Assert.Equal(2, upgraded.Count);
        Assert.Equal(4, upgraded.Level);

        // The raised laser rate shows in play: 1.25 volleys per second.
        var fired = 0;
        for (var i = 0; i < 240; i++)
        {
            var count = session.Lasers.Count;
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            if (session.Lasers.Count > count)
                fired++;
        }
        Assert.InRange(fired, 4, 5);
    }

    [Fact]
    public void An_upgrade_card_carries_one_option_and_two_cards_for_one_weapon_always_differ()
    {
        var sawTwoLaserCards = false;
        for (var seed = 0; seed < 200; seed++)
        {
            var stats = new PlayerStats();
            stats.AddWeapon(WeaponKind.Laser);
            var offer = Upgrades.Offer(stats, new Random(seed), GameSession.UpgradeChoices);
            var laserCards = offer.Where(card => card.Kind == UpgradeKind.WeaponUpgrade).ToArray();
            Assert.All(laserCards, card => Assert.Contains(card.Option!.Value, Upgrades.OptionsFor(WeaponKind.Laser)));
            Assert.Equal(laserCards.Length, laserCards.Select(card => card.Option).Distinct().Count());
            sawTwoLaserCards |= laserCards.Length >= 2;
        }
        Assert.True(sawTwoLaserCards, "No offer held two laser cards");
    }

    [Fact]
    public void Card_text_names_the_weapon_the_effect_and_the_level_change()
    {
        var stats = new PlayerStats();
        stats.AddWeapon(WeaponKind.Laser);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage), stats);
        Assert.Equal(2, stats.Laser().Level);

        var damage = Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage);
        var text = $"{Upgrades.Title(damage, stats)} {Upgrades.Describe(damage, stats)}";
        Assert.Contains("LASER", text);
        Assert.Contains("+1 DAMAGE", text);
        Assert.Contains("LV 2 > 3", text);

        Assert.Equal("+1 PROJECTILE", Upgrades.Describe(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserProjectile), stats));
        Assert.Equal("+0.25 ATTACKS/S", Upgrades.Describe(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserAttackSpeed), stats));

        var lightning = Upgrade.NewWeapon(WeaponKind.Lightning);
        Assert.Equal("LIGHTNING", Upgrades.Title(lightning, stats));
        Assert.Contains("NEW", Upgrades.Describe(lightning, stats));

        // Every option of every weapon has card text the vector font can draw (three weapons per ship, all five covered).
        foreach (var kinds in Upgrades.AllWeapons.Chunk(PlayerStats.MaxWeapons))
        {
            var all = new PlayerStats();
            foreach (var kind in kinds)
                all.AddWeapon(kind);
            foreach (var card in Upgrades.Eligible(all).Where(card => card.Kind == UpgradeKind.WeaponUpgrade))
            {
                var cardText = $"{Upgrades.Title(card, all)} {Upgrades.Describe(card, all)}";
                Assert.Contains(Upgrades.WeaponName(card.Weapon!.Value), cardText);
                Assert.Contains("LV 1 > 2", cardText);
                Assert.NotEmpty(VectorText.GetStrokes(cardText, Vector2.Zero, 10f));
            }
        }
        foreach (var kind in Upgrades.AllWeapons)
        {
            var card = Upgrade.NewWeapon(kind);
            Assert.NotEmpty(VectorText.GetStrokes($"{Upgrades.Title(card, stats)} {Upgrades.Describe(card, stats)}", Vector2.Zero, 10f));
        }
    }

    [Fact]
    public void Ricochet_disc_and_proximity_mines_new_weapon_cards_carry_their_exact_title_and_description()
    {
        var stats = new PlayerStats();
        var disc = Upgrade.NewWeapon(WeaponKind.RicochetDisc);
        var mines = Upgrade.NewWeapon(WeaponKind.ProximityMines);

        Assert.Equal("RICOCHET DISC", Upgrades.Title(disc, stats));
        Assert.Equal(
            "Throws a disc that flies out and back, cutting through every enemy.",
            Upgrades.Describe(disc, stats));
        Assert.DoesNotContain("SHATTER", Upgrades.Describe(disc, stats));

        Assert.Equal("PROXIMITY MINES", Upgrades.Title(mines, stats));
        Assert.Equal(
            "Drops mines that explode when enemies touch them.",
            Upgrades.Describe(mines, stats));
        Assert.DoesNotContain("CLUSTER", Upgrades.Describe(mines, stats));

        // The three original weapons keep their plain NEW WEAPON line.
        Assert.Equal("NEW WEAPON", Upgrades.Describe(Upgrade.NewWeapon(WeaponKind.Orbs), stats));
    }

    [Fact]
    public void Ricochet_disc_and_proximity_mines_join_the_new_weapon_pool_only_while_unowned_and_a_slot_is_free()
    {
        var (sawDisc, sawMines) = (false, false);
        for (var seed = 0; seed < 300; seed++)
        {
            // One weapon owned: both new weapons can be offered.
            var one = new PlayerStats();
            one.AddWeapon(WeaponKind.Laser);
            var offer = Upgrades.Offer(one, new Random(seed), GameSession.UpgradeChoices);
            sawDisc |= offer.Contains(Upgrade.NewWeapon(WeaponKind.RicochetDisc));
            sawMines |= offer.Contains(Upgrade.NewWeapon(WeaponKind.ProximityMines));

            // The disc owned: its new-weapon card never shows, the mines' still may.
            var owningDisc = new PlayerStats();
            owningDisc.AddWeapon(WeaponKind.RicochetDisc);
            Assert.DoesNotContain(Upgrade.NewWeapon(WeaponKind.RicochetDisc), Upgrades.Offer(owningDisc, new Random(seed), GameSession.UpgradeChoices));

            // Three weapons owned: neither new weapon shows.
            var full = new PlayerStats();
            full.AddWeapon(WeaponKind.Laser);
            full.AddWeapon(WeaponKind.Lightning);
            full.AddWeapon(WeaponKind.Orbs);
            Assert.DoesNotContain(Upgrades.Offer(full, new Random(seed), GameSession.UpgradeChoices), card => card.Kind == UpgradeKind.NewWeapon);
        }
        Assert.True(sawDisc, "No offer held a Ricochet Disc card");
        Assert.True(sawMines, "No offer held a Proximity Mines card");

        var withDisc = new PlayerStats();
        withDisc.AddWeapon(WeaponKind.RicochetDisc);
        Assert.Contains(Upgrade.NewWeapon(WeaponKind.ProximityMines), Upgrades.Eligible(withDisc));
        Assert.DoesNotContain(Upgrade.NewWeapon(WeaponKind.RicochetDisc), Upgrades.Eligible(withDisc));
    }

    [Fact]
    public void Each_ricochet_disc_option_changes_only_its_stat_and_one_level_and_a_level_5_disc_gets_no_card()
    {
        var expected = new Dictionary<WeaponOption, Func<WeaponSnapshotFull, WeaponSnapshotFull>>
        {
            [WeaponOption.DiscCount] = s => s with { Count = s.Count + 1 },
            [WeaponOption.DiscDamage] = s => s with { Damage = s.Damage + 1f },
            [WeaponOption.DiscAttackSpeed] = s => s with { AttacksPerSecond = s.AttacksPerSecond + 0.15f },
            [WeaponOption.DiscSize] = s => s with { SizeScale = s.SizeScale + 0.25f },
        };
        AssertEachOptionChangesOnlyItsStat(WeaponKind.RicochetDisc, expected,
            new WeaponSnapshotFull(1, 2f, 0.5f, 1, 1f, 250f, 400f, 0f));
        Assert.Equal(["+1 DISC", "+1 DAMAGE", "+0.15 ATTACKS/S", "+25% DISC SIZE"],
            Upgrades.OptionsFor(WeaponKind.RicochetDisc).Select(Upgrades.Effect));
        AssertAllOptionsOfferedDistinctAndNoneAtLevel5(WeaponKind.RicochetDisc);
    }

    [Fact]
    public void Each_proximity_mines_option_changes_only_its_stat_and_one_level_and_a_level_5_mines_weapon_gets_no_card()
    {
        var expected = new Dictionary<WeaponOption, Func<WeaponSnapshotFull, WeaponSnapshotFull>>
        {
            [WeaponOption.MinesDamage] = s => s with { Damage = s.Damage + 1.5f },
            [WeaponOption.MinesBlastRadius] = s => s with { BlastRadius = s.BlastRadius + 15f },
            [WeaponOption.MinesDropRate] = s => s with { AttacksPerSecond = s.AttacksPerSecond + 0.15f },
            [WeaponOption.MinesPerDrop] = s => s with { Count = s.Count + 1 },
        };
        AssertEachOptionChangesOnlyItsStat(WeaponKind.ProximityMines, expected,
            new WeaponSnapshotFull(1, 3f, 0.5f, 1, 0f, 0f, 0f, 60f));
        Assert.Equal(["+1.5 DAMAGE", "+15 PX BLAST RADIUS", "+0.15 DROPS/S", "+1 MINE PER DROP"],
            Upgrades.OptionsFor(WeaponKind.ProximityMines).Select(Upgrades.Effect));
        AssertAllOptionsOfferedDistinctAndNoneAtLevel5(WeaponKind.ProximityMines);

        // One drop lays one mine per Count: the first at the ship, every extra one within 40 px of it.
        var stats = new PlayerStats();
        var mines = stats.AddWeapon(WeaponKind.ProximityMines);
        Assert.Equal([Center], mines.MineDropPositions(Center, new Random(1)));
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.ProximityMines, WeaponOption.MinesPerDrop), stats);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.ProximityMines, WeaponOption.MinesPerDrop), stats);
        var sawSpread = false;
        for (var seed = 0; seed < 50; seed++)
        {
            var drop = mines.MineDropPositions(Center, new Random(seed));
            Assert.Equal(3, drop.Count);
            Assert.Equal(Center, drop[0]);
            Assert.All(drop, position => Assert.InRange(Vector2.Distance(position, Center), 0f, OwnedWeapon.MineScatterRadius + 0.001f));
            sawSpread |= drop.Skip(1).Any(position => Vector2.Distance(position, Center) > 1f);
        }
        Assert.True(sawSpread, "Extra mines never left the ship's spot");
    }

    private sealed record WeaponSnapshotFull(int Level, float Damage, float AttacksPerSecond, int Count, float SizeScale, float Range, float Speed, float BlastRadius)
    {
        public static WeaponSnapshotFull Of(OwnedWeapon w) =>
            new(w.Level, w.Damage, w.AttacksPerSecond, w.Count, w.SizeScale, w.Range, w.Speed, w.BlastRadius);

        public WeaponSnapshotFull Rounded() => this with
        {
            Damage = MathF.Round(Damage, 4),
            AttacksPerSecond = MathF.Round(AttacksPerSecond, 4),
            SizeScale = MathF.Round(SizeScale, 4),
            BlastRadius = MathF.Round(BlastRadius, 4),
        };
    }

    /// <summary>
    /// For each option: a fresh ship owning a level-1 weapon of this kind beside a laser and lightning takes that one
    /// card; only the option's stat moves by its step, the level rises by one, and the other weapons don't change.
    /// </summary>
    private static void AssertEachOptionChangesOnlyItsStat(
        WeaponKind kind, IReadOnlyDictionary<WeaponOption, Func<WeaponSnapshotFull, WeaponSnapshotFull>> expected, WeaponSnapshotFull levelOne)
    {
        Assert.Equal(Upgrades.OptionsFor(kind).ToHashSet(), expected.Keys.ToHashSet());
        foreach (var (option, change) in expected)
        {
            var stats = new PlayerStats();
            var laser = stats.AddWeapon(WeaponKind.Laser);
            var weapon = stats.AddWeapon(kind);
            var lightning = stats.AddWeapon(WeaponKind.Lightning);
            var before = WeaponSnapshotFull.Of(weapon);
            Assert.Equal(levelOne, before);
            var (laserBefore, lightningBefore) = (WeaponSnapshotFull.Of(laser), WeaponSnapshotFull.Of(lightning));

            Upgrades.Apply(Upgrade.ForWeapon(kind, option), stats);

            Assert.Equal(change(before with { Level = 2 }).Rounded(), WeaponSnapshotFull.Of(weapon).Rounded());
            Assert.Equal(laserBefore, WeaponSnapshotFull.Of(laser));
            Assert.Equal(lightningBefore, WeaponSnapshotFull.Of(lightning));
        }
    }

    /// <summary>
    /// Across seeded offers every option of the weapon appears, two cards for it in one offer always differ, and once
    /// it reaches level 20 no card for it is offered (and applying one is refused).
    /// </summary>
    private static void AssertAllOptionsOfferedDistinctAndNoneAtLevel5(WeaponKind kind)
    {
        var seen = new HashSet<WeaponOption>();
        var sawTwo = false;
        for (var seed = 0; seed < 300; seed++)
        {
            var stats = new PlayerStats();
            stats.AddWeapon(kind);
            var cards = Upgrades.Offer(stats, new Random(seed), GameSession.UpgradeChoices)
                .Where(card => card.Kind == UpgradeKind.WeaponUpgrade && card.Weapon == kind).ToArray();
            Assert.Equal(cards.Length, cards.Select(card => card.Option).Distinct().Count());
            sawTwo |= cards.Length >= 2;
            seen.UnionWith(cards.Select(card => card.Option!.Value));
        }
        Assert.Equal(Upgrades.OptionsFor(kind).ToHashSet(), seen);
        Assert.True(sawTwo, $"No offer held two {kind} cards");

        var maxed = new PlayerStats();
        var weapon = maxed.AddWeapon(kind);
        var secondKind = kind == WeaponKind.Laser ? WeaponKind.Lightning : WeaponKind.Laser;
        maxed.AddWeapon(secondKind);
        var options = Upgrades.OptionsFor(kind);

        for (var l = 1; l < OwnedWeapon.MaxLevel; l++)
        {
            var option = options[(l - 1) % options.Count];
            var card = Upgrade.ForWeapon(kind, option);

            // At every level below 20 it asserts the card is among the options
            var eligible = Upgrades.Eligible(maxed);
            Assert.Contains(card, eligible);

            var statBefore = GetWeaponOptionStat(weapon, option);
            var step = GetWeaponOptionStep(option);

            Upgrades.Apply(card, maxed);

            // After applying it asserts Level == L+1 and the stat equals the per-level formula
            Assert.Equal(l + 1, weapon.Level);
            var statAfter = GetWeaponOptionStat(weapon, option);
            Assert.Equal(statBefore + step, statAfter, precision: 3);

            if (l == 5)
            {
                // Explicit assert at 5->6
                Assert.Equal(5, l);
                Assert.Equal(6, weapon.Level);
            }
            if (l == 19)
            {
                // Explicit assert at 19->20
                Assert.Equal(19, l);
                Assert.Equal(20, weapon.Level);
                Assert.Equal(OwnedWeapon.MaxLevel, weapon.Level);
            }
        }

        Assert.Equal(OwnedWeapon.MaxLevel, weapon.Level);
        for (var seed = 0; seed < 300; seed++)
            Assert.DoesNotContain(Upgrades.Offer(maxed, new Random(seed), GameSession.UpgradeChoices), card => card.Weapon == kind);
        Assert.DoesNotContain(Upgrades.Eligible(maxed), card => card.Weapon == kind);
        var ex = Assert.Throws<InvalidOperationException>(() => Upgrades.Apply(Upgrade.ForWeapon(kind, options[0]), maxed));
        Assert.Contains("20", ex.Message);
    }

    [Theory]
    [InlineData(WeaponKind.Laser)]
    [InlineData(WeaponKind.Lightning)]
    [InlineData(WeaponKind.Orbs)]
    [InlineData(WeaponKind.RicochetDisc)]
    [InlineData(WeaponKind.ProximityMines)]
    public void For_every_weapon_kind_ship_is_offered_upgrades_up_to_level_20_and_applies_standard_step(WeaponKind kind)
    {
        AssertAllOptionsOfferedDistinctAndNoneAtLevel5(kind);
    }

    [Fact]
    public void Weapon_card_titles_show_levels_up_to_level_19_to_20()
    {
        var stats = new PlayerStats();
        stats.AddWeapon(WeaponKind.Laser);
        var laser = stats.Laser();

        var toTwo = Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage);
        Assert.Equal("LASER LV 1 > 2", Upgrades.Title(toTwo, stats));

        while (laser.Level < 5)
            Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage), stats);
        Assert.Equal(5, laser.Level);
        var toSix = Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage);
        Assert.Equal("LASER LV 5 > 6", Upgrades.Title(toSix, stats));

        while (laser.Level < 19)
            Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage), stats);
        Assert.Equal(19, laser.Level);
        var toTwenty = Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage);
        Assert.Equal("LASER LV 19 > 20", Upgrades.Title(toTwenty, stats));
    }

    [Fact]
    public void When_all_three_weapon_and_three_enhancement_slots_are_filled_only_upgrades_are_offered()
    {
        var stats = new PlayerStats();
        stats.AddWeapon(WeaponKind.Laser);
        stats.AddWeapon(WeaponKind.Lightning);
        stats.AddWeapon(WeaponKind.Orbs);

        stats.AddEnhancement(ShipEnhancements.MoveSpeed);
        stats.AddEnhancement(ShipEnhancements.Health);
        stats.AddEnhancement(ShipEnhancements.Shield);

        // All 3 weapons and 3 enhancements owned below level 20
        Assert.Equal(3, stats.Weapons.Count);
        Assert.Equal(3, stats.Enhancements.Count);
        Assert.All(stats.Weapons, w => Assert.True(w.Level < OwnedWeapon.MaxLevel));
        Assert.All(stats.Enhancements, e => Assert.True(e.Level < OwnedEnhancement.MaxLevel));

        var eligible = Upgrades.Eligible(stats);
        Assert.NotEmpty(eligible);
        Assert.DoesNotContain(eligible, card => card.Kind == UpgradeKind.NewWeapon);
        Assert.DoesNotContain(eligible, card => card.Kind == UpgradeKind.NewEnhancement);
        Assert.All(eligible, card => Assert.True(card.Kind is UpgradeKind.WeaponUpgrade or UpgradeKind.EnhancementUpgrade));

        for (var seed = 0; seed < 100; seed++)
        {
            var offer = Upgrades.Offer(stats, new Random(seed), GameSession.UpgradeChoices);
            Assert.NotEmpty(offer);
            Assert.DoesNotContain(offer, card => card.Kind == UpgradeKind.NewWeapon);
            Assert.DoesNotContain(offer, card => card.Kind == UpgradeKind.NewEnhancement);
            Assert.All(offer, card => Assert.True(card.Kind is UpgradeKind.WeaponUpgrade or UpgradeKind.EnhancementUpgrade));
        }
    }

    [Fact]
    public void When_every_owned_weapon_and_enhancement_is_at_level_20_no_cards_offered_and_apply_refused()
    {
        var stats = new PlayerStats();
        foreach (var kind in Upgrades.AllWeapons.Take(PlayerStats.MaxWeapons))
            stats.AddWeapon(kind);
        foreach (var enhancement in ShipEnhancements.All.Take(PlayerStats.MaxEnhancements))
            stats.AddEnhancement(enhancement);

        foreach (var weapon in stats.Weapons)
        {
            while (weapon.Level < OwnedWeapon.MaxLevel)
                Upgrades.Apply(Upgrade.ForWeapon(weapon.Kind, Upgrades.OptionsFor(weapon.Kind)[0]), stats);
            Assert.Equal(20, weapon.Level);
        }

        foreach (var enhancement in stats.Enhancements)
        {
            while (enhancement.Level < OwnedEnhancement.MaxLevel)
                Upgrades.Apply(Upgrade.ForEnhancement(enhancement.Definition), stats);
            Assert.Equal(20, enhancement.Level);
        }

        var eligible = Upgrades.Eligible(stats);
        Assert.Empty(eligible);

        for (var seed = 0; seed < 100; seed++)
        {
            var offer = Upgrades.Offer(stats, new Random(seed), GameSession.UpgradeChoices);
            Assert.Empty(offer);
        }

        foreach (var weapon in stats.Weapons)
        {
            var card = Upgrade.ForWeapon(weapon.Kind, Upgrades.OptionsFor(weapon.Kind)[0]);
            var ex = Assert.Throws<InvalidOperationException>(() => Upgrades.Apply(card, stats));
            Assert.Contains("20", ex.Message);
            Assert.Equal(20, weapon.Level);
        }

        foreach (var enhancement in stats.Enhancements)
        {
            var card = Upgrade.ForEnhancement(enhancement.Definition);
            var ex = Assert.Throws<InvalidOperationException>(() => Upgrades.Apply(card, stats));
            Assert.Contains("20", ex.Message);
            Assert.Equal(20, enhancement.Level);
        }
    }

    private static float GetWeaponOptionStat(OwnedWeapon weapon, WeaponOption option) => option switch
    {
        WeaponOption.LaserProjectile => weapon.Count,
        WeaponOption.LaserDamage => weapon.Damage,
        WeaponOption.LaserAttackSpeed => weapon.AttacksPerSecond,
        WeaponOption.LightningDamage => weapon.Damage,
        WeaponOption.LightningAttackSpeed => weapon.AttacksPerSecond,
        WeaponOption.LightningChain => weapon.Count,
        WeaponOption.OrbsCount => weapon.Count,
        WeaponOption.OrbsSpeedAndOrbit => weapon.Orbs?.OrbitRadius ?? 0f,
        WeaponOption.OrbsRadius => weapon.Orbs?.OrbRadius ?? 0f,
        WeaponOption.OrbsDamage => weapon.Damage,
        WeaponOption.DiscCount => weapon.Count,
        WeaponOption.DiscDamage => weapon.Damage,
        WeaponOption.DiscAttackSpeed => weapon.AttacksPerSecond,
        WeaponOption.DiscSize => weapon.SizeScale,
        WeaponOption.MinesDamage => weapon.Damage,
        WeaponOption.MinesBlastRadius => weapon.BlastRadius,
        WeaponOption.MinesDropRate => weapon.AttacksPerSecond,
        WeaponOption.MinesPerDrop => weapon.Count,
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };

    private static float GetWeaponOptionStep(WeaponOption option) => option switch
    {
        WeaponOption.LaserProjectile => 1f,
        WeaponOption.LaserDamage => Upgrades.LaserDamageStep,
        WeaponOption.LaserAttackSpeed => Upgrades.LaserAttackSpeedStep,
        WeaponOption.LightningDamage => Upgrades.LightningDamageStep,
        WeaponOption.LightningAttackSpeed => Upgrades.LightningAttackSpeedStep,
        WeaponOption.LightningChain => 1f,
        WeaponOption.OrbsCount => OrbitingOrbs.UpgradeOrbCountStep,
        WeaponOption.OrbsSpeedAndOrbit => OrbitingOrbs.UpgradeOrbitRadiusStep,
        WeaponOption.OrbsRadius => OrbitingOrbs.UpgradeOrbRadiusStep,
        WeaponOption.OrbsDamage => OrbitingOrbs.UpgradeDamageStep,
        WeaponOption.DiscCount => 1f,
        WeaponOption.DiscDamage => Upgrades.DiscDamageStep,
        WeaponOption.DiscAttackSpeed => Upgrades.DiscAttackSpeedStep,
        WeaponOption.DiscSize => Upgrades.DiscSizeStep,
        WeaponOption.MinesDamage => Upgrades.MinesDamageStep,
        WeaponOption.MinesBlastRadius => Upgrades.MinesBlastRadiusStep,
        WeaponOption.MinesDropRate => Upgrades.MinesDropRateStep,
        WeaponOption.MinesPerDrop => 1f,
        _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };

    [Fact]
    public void Killing_enough_enemies_pauses_play_until_an_upgrade_is_chosen()
    {
        var session = TestEnemies.QuietSession(Center);
        // Gemini is worth 2 experience; level 1 needs 2, so the first kill levels up.
        for (var i = 0; i < 3; i++)
            session.AddEnemy(TestEnemies.StationaryGemini(Center + new Vector2(0, -120 - 60 * i)));
        session.Stats.Laser().Damage = 10f;
        session.Stats.Laser().Pierce = 5;

        for (var i = 0; i < 60 * 3 && !session.IsChoosingUpgrade; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.True(session.IsChoosingUpgrade);
        Assert.Equal(2, session.Leveling.Level);
        var offer = session.UpgradeOffer!;
        Assert.Equal(3, offer.Count);

        var clock = session.ElapsedSeconds;
        var ship = session.Ship.Position;
        session.Update(new Vector2(0, -1), null, 1f, Width, Height);
        Assert.Equal(clock, session.ElapsedSeconds);
        Assert.Equal(ship, session.Ship.Position);

        var before = Snapshot(session.Stats);
        session.ChooseUpgrade(0);

        Assert.False(session.IsChoosingUpgrade);
        Assert.NotEqual(before, Snapshot(session.Stats));
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.True(session.ElapsedSeconds > clock);
    }

    [Fact]
    public void Several_levels_at_once_offer_cards_one_level_at_a_time()
    {
        var session = TestEnemies.QuietSession(Center);
        // One kill worth 18 experience: 2 for level 2, 5 for level 3 and 8 for level 4, with 3 left over.
        var bigPrize = EnemyRoster.ChatGpt with { Experience = 18 };
        session.AddEnemy(TestEnemies.Stationary(bigPrize, Center + new Vector2(0, -150)));
        session.Stats.Laser().Damage = 100f;

        for (var i = 0; i < 60 * 2 && !session.IsChoosingUpgrade; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Equal(4, session.Leveling.Level);
        Assert.Equal(3, session.Leveling.Experience);
        var clock = session.ElapsedSeconds;

        // Three regular rounds in a row, one per level gained, with play frozen between them.
        for (var round = 0; round < 3; round++)
        {
            Assert.True(session.IsChoosingUpgrade, $"Round {round + 1} wasn't offered");
            Assert.False(session.IsStartingPick);
            Assert.Equal(GameSession.UpgradeChoices, session.UpgradeOffer!.Count);
            session.Update(Vector2.Zero, null, 1f, Width, Height);
            Assert.Equal(clock, session.ElapsedSeconds);
            session.ChooseUpgrade(1);
        }

        Assert.False(session.IsChoosingUpgrade);
        Assert.Throws<InvalidOperationException>(() => session.ChooseUpgrade(0));
    }

    [Fact]
    public void Attack_speed_fires_volleys_more_often()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.Laser().AttacksPerSecond = 2.5f;
        var volleys = 0;

        // Count volleys over 2 seconds; lasers fly up and stay on screen long enough to count.
        for (var i = 0; i < 120; i++)
        {
            var before = session.Lasers.Count;
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            if (session.Lasers.Count > before)
                volleys++;
        }

        Assert.Equal(5, volleys);
    }

    [Fact]
    public void Firing_rate_change_keeps_progress_toward_the_next_shot()
    {
        var timer = new FiringTimer();

        Assert.Empty(timer.Update(0.5f, Vector2.Zero, 0f, shotsPerSecond: 1f));
        Assert.Single(timer.Update(0.25f, Vector2.Zero, 0f, shotsPerSecond: 2f));
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.Update(0.1f, Vector2.Zero, 0f, shotsPerSecond: 0f));
    }

    [Fact]
    public void Volley_fans_lasers_evenly_around_the_heading()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.Laser().Count = 3;

        session.Update(Vector2.Zero, null, 1f, Width, Height);

        var headings = session.Lasers.Select(laser => laser.Heading).OrderBy(h => h).ToArray();
        Assert.Equal(3, headings.Length);
        Assert.Equal(-GameSession.VolleySpreadRadians, headings[0], precision: 5);
        Assert.Equal(0f, headings[1], precision: 5);
        Assert.Equal(GameSession.VolleySpreadRadians, headings[2], precision: 5);
    }

    [Fact]
    public void Projectile_size_scales_laser_width_and_length_together()
    {
        var laser = new Laser(Center, 0f, scale: 2f);

        Assert.Equal(26f, laser.Length);
        Assert.Equal(8f, laser.Width);
        Assert.Equal(26f, Vector2.Distance(laser.GetLine()[0], laser.GetLine()[1]), precision: 3);
    }

    [Fact]
    public void Wider_laser_hits_an_enemy_a_thin_one_just_misses()
    {
        var outline = new[] { new[] { new Vector2(10, -5), new Vector2(10, 5) } };

        Assert.False(Collision.ThickSegmentTouches(new(0, -20), new(0, 20), 1f, outline));
        Assert.True(Collision.ThickSegmentTouches(new(0, -20), new(0, 20), 10f, outline));
    }

    [Fact]
    public void Piercing_laser_passes_through_to_hit_the_enemy_behind()
    {
        var session = TestEnemies.QuietSession(Center);
        var front = TestEnemies.StationaryGemini(Center + new Vector2(0, -120));
        var back = TestEnemies.StationaryGemini(Center + new Vector2(0, -200));
        session.AddEnemy(front);
        session.AddEnemy(back);
        session.Stats.Laser().Pierce = 1;

        for (var i = 0; i < 90 && back.HitPoints == 3f; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Equal(2f, front.HitPoints);
        Assert.Equal(2f, back.HitPoints);
    }

    // ---- Ship hit points and contact damage ----

    [Fact]
    public void A_new_session_starts_at_100_of_100_hit_points_whatever_the_start_options()
    {
        foreach (var start in new[] { StartOptions.Default, new StartOptions(StartLevel: 5) })
        {
            var session = new GameSession(Center, new Random(1), start: start);
            Assert.Equal(100f, session.Stats.MaxHitPoints);
            Assert.Equal(100f, session.Stats.HitPoints);
        }
    }

    [Fact]
    public void Touching_enemy_survives_and_deals_a_tick_at_once_then_every_half_second_with_no_immunity()
    {
        var session = TestEnemies.QuietSession(Center);
        var enemy = TestEnemies.Stationary(Tough, Center);
        session.AddEnemy(enemy);

        // First touch: one tick at once, and the enemy stays.
        var ticks = StepCountingTicks(session, frames: 1);
        Assert.Equal(1, ticks);
        Assert.Equal(100f - EnemyRoster.ScaledContactDamage(Tough.ContactDamage, Frame), session.Stats.HitPoints);
        Assert.Contains(enemy, session.Enemies);

        // Staying in contact for 1.0 s more: exactly two more ticks, each the damage scaled for when it landed.
        Assert.Equal(2, StepCountingTicks(session, frames: 60));
        Assert.Contains(enemy, session.Enemies);

        // Fly clear (0.3 s, before the next tick is due) and stay clear: no more losses.
        for (var i = 0; i < 18; i++)
            session.Update(new Vector2(1, 0), null, Frame, Width, Height);
        var clear = session.Stats.HitPoints;
        Assert.Equal(0, StepCountingTicks(session, frames: 120));
        Assert.Equal(clear, session.Stats.HitPoints);
    }

    [Fact]
    public void Contact_timer_clears_when_contact_ends_so_a_fresh_touch_ticks_at_once()
    {
        var session = TestEnemies.QuietSession(Center + new Vector2(-100, 0));
        session.AddEnemy(TestEnemies.Stationary(Tough, Center));

        // Fly through: first contact ticks at once.
        var ticks = 0;
        for (var i = 0; i < 30 && ticks == 0; i++)
            ticks += StepCountingTicks(session, frames: 1, direction: new Vector2(1, 0));
        Assert.Equal(1, ticks);
        var firstTickAt = session.ElapsedSeconds;

        // Back off out of reach, then fly back: the touch ticks again at once, well before the old 0.5 s timer would.
        while (Vector2.Distance(session.Ship.Position, Center) < 80f)
            session.Update(new Vector2(-1, 0), null, Frame, Width, Height);
        var again = 0;
        for (var i = 0; i < 60 && again == 0; i++)
            again += StepCountingTicks(session, frames: 1, direction: new Vector2(1, 0));
        Assert.Equal(1, again);
        Assert.True(session.ElapsedSeconds - firstTickAt < GameSession.ContactTickSeconds - 0.05f,
            $"Second touch ticked {session.ElapsedSeconds - firstTickAt} s after the first");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(50)]
    public void A_swarm_costs_one_tick_per_touching_enemy_on_the_first_frame(int count)
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 10_000f;
        session.Stats.HitPoints = 10_000f;
        for (var i = 0; i < count; i++)
            session.AddEnemy(TestEnemies.Stationary(Tough, Center));

        session.Update(Vector2.Zero, null, Frame, Width, Height);

        var tick = EnemyRoster.ScaledContactDamage(Tough.ContactDamage, Frame);
        Assert.Equal(10_000f - count * tick, session.Stats.HitPoints, precision: 2);
        Assert.Equal(count, session.Enemies.Count);
    }

    [Fact]
    public void No_touching_source_costs_nothing()
    {
        var session = TestEnemies.QuietSession(Center);
        for (var i = 0; i < 300; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Equal(100f, session.Stats.HitPoints);
        Assert.False(session.IsGameOver);
    }

    [Fact]
    public void Hit_points_are_stored_to_the_hundredth_clamped_and_shown_rounded_up()
    {
        var stats = new PlayerStats();

        stats.HitPoints = 57.456f;
        Assert.Equal(57.46f, stats.HitPoints);
        Assert.Equal(58, stats.DisplayHitPoints);

        stats.HitPoints = -3f;
        Assert.Equal(0f, stats.HitPoints);
        Assert.Equal(0, stats.DisplayHitPoints);

        stats.HitPoints = 150f;
        Assert.Equal(100f, stats.HitPoints);
        Assert.Equal(100, stats.DisplayHitPoints);
        Assert.Equal(100, stats.DisplayMaxHitPoints);

        stats.HitPoints = 0.01f;
        Assert.Equal(1, stats.DisplayHitPoints);

        // Lowering the maximum below the current value pulls the current value down with it.
        stats.HitPoints = 80f;
        stats.MaxHitPoints = 60f;
        Assert.Equal(60f, stats.HitPoints);
    }

    [Fact]
    public void A_tick_at_a_fractional_scale_leaves_hit_points_a_whole_number_of_hundredths()
    {
        // At 1:17 a 7-damage enemy's tick is 7 x (1 + 77/600) = 7.898..., dealt as 7.90.
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], rings: [],
            start: new StartOptions(StartSeconds: 77f)).WithLaser();
        session.AddEnemy(TestEnemies.Stationary(Tough with { ContactDamage = 7f }, Center));

        session.Update(Vector2.Zero, null, Frame, Width, Height);

        var hundredths = session.Stats.HitPoints * 100f;
        Assert.Equal(MathF.Round(hundredths), hundredths, precision: 2);
        Assert.True(session.Stats.HitPoints < 100f);
    }

    /// <summary>A Gemini that a stray laser can't kill during a contact test.</summary>
    private static readonly EnemyType Tough = EnemyRoster.Gemini with { HitPoints = 1_000_000f };

    /// <summary>Steps frame by frame and counts the frames whose hit point loss matched one tick scaled for that moment.</summary>
    private static int StepCountingTicks(GameSession session, int frames, Vector2? direction = null)
    {
        var ticks = 0;
        for (var i = 0; i < frames; i++)
        {
            var before = session.Stats.HitPoints;
            session.Update(direction ?? Vector2.Zero, null, Frame, Width, Height);
            var lost = before - session.Stats.HitPoints;
            if (lost <= 0f)
                continue;
            Assert.Equal(EnemyRoster.ScaledContactDamage(Tough.ContactDamage, session.ElapsedSeconds), lost, precision: 2);
            ticks++;
        }
        return ticks;
    }

    [Fact]
    public void Enemy_types_join_the_spawner_at_their_arrival_times()
    {
        var session = new GameSession(Center, new Random(1)).WithLaser();
        Assert.Equal([EnemyRoster.Gemini], session.Spawner.Types);
        session.Stats.MaxHitPoints = 1_000_000f;
        session.Stats.HitPoints = 1_000_000f;
        var seen = new HashSet<Enemy>();

        void Step()
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            while (session.IsChoosingUpgrade)
                session.ChooseUpgrade(0);
            seen.UnionWith(session.Enemies);
        }

        // Until just before the first boss at 3:00, Gemini is the only type that has joined or spawned.
        while (session.ElapsedSeconds < 179.9f)
            Step();
        Assert.NotEmpty(seen);
        Assert.All(seen, enemy => Assert.Same(EnemyRoster.Gemini, enemy.Type));
        Assert.Equal([EnemyRoster.Gemini], session.Spawner.Types);

        // Hugging Face joins as the Gemini Boss arrives, but nothing regular spawns while the boss lives.
        while (!session.IsBossActive)
            Step();
        var boss = Assert.Single(session.Bosses);
        Assert.Contains(EnemyRoster.HuggingFace, session.Spawner.Types);
        for (var i = 0; i < 60 * 5; i++)
            Step();
        Assert.DoesNotContain(seen, enemy => ReferenceEquals(enemy.Type, EnemyRoster.HuggingFace));

        // Once it is dead, Hugging Face appears; ChatGPT has not joined yet.
        foreach (var shield in boss.Shields)
            shield.TakeDamage(shield.HitPoints);
        boss.TakeDamage(boss.HitPoints);
        for (var i = 0; i < 60 * 5; i++)
            Step();
        Assert.False(session.IsBossActive);
        Assert.Contains(seen, enemy => ReferenceEquals(enemy.Type, EnemyRoster.HuggingFace));
        Assert.DoesNotContain(EnemyRoster.ChatGpt, session.Spawner.Types);
        Assert.DoesNotContain(seen, enemy => ReferenceEquals(enemy.Type, EnemyRoster.ChatGpt));
    }

    [Fact]
    public void Upgrade_cards_lay_out_side_by_side_and_clicks_find_the_right_card()
    {
        var corners = UpgradeCards.Layout(Center, 3);

        Assert.Equal(3, corners.Count);
        Assert.Equal(Center.X, (corners[0].X + corners[2].X + UpgradeCards.CardSize.X) / 2f, precision: 3);
        Assert.Equal(0, UpgradeCards.HitTest(Center, 3, corners[0] + new Vector2(5, 5)));
        Assert.Equal(1, UpgradeCards.HitTest(Center, 3, Center));
        Assert.Equal(2, UpgradeCards.HitTest(Center, 3, corners[2] + UpgradeCards.CardSize - new Vector2(5, 5)));
        Assert.Null(UpgradeCards.HitTest(Center, 3, corners[0] + new Vector2(UpgradeCards.CardSize.X + 5, 5)));
    }

    [Fact]
    public void Each_health_level_applied_raises_max_and_current_hit_points_by_20()
    {
        // From 100/100, Health level 1 gives 120/120.
        var full = new PlayerStats();
        Upgrades.Apply(Upgrade.NewEnhancement(ShipEnhancements.Health), full);
        Assert.Equal(120f, full.MaxHitPoints);
        Assert.Equal(120f, full.HitPoints);

        // From 50/100, Health level 1 gives 70/120.
        var damaged = new PlayerStats();
        damaged.HitPoints = 50f;
        Upgrades.Apply(Upgrade.NewEnhancement(ShipEnhancements.Health), damaged);
        Assert.Equal(120f, damaged.MaxHitPoints);
        Assert.Equal(70f, damaged.HitPoints);

        // Five levels from 100/100 give 200/200.
        var maxed = new PlayerStats();
        Upgrades.Apply(Upgrade.NewEnhancement(ShipEnhancements.Health), maxed);
        for (var i = 0; i < 4; i++)
            Upgrades.Apply(Upgrade.ForEnhancement(ShipEnhancements.Health), maxed);
        Assert.Equal(200f, maxed.MaxHitPoints);
        Assert.Equal(200f, maxed.HitPoints);
        Assert.Equal(5, maxed.Find(ShipEnhancements.Health)!.Level);
    }

    [Fact]
    public void Regeneration_heals_every_second_and_a_hit_never_multiplies_it_even_at_level_20()
    {
        // Level 1 at 50/100 gives 50.20 after 1 s and 51.00 after 5 s.
        var session = TestEnemies.QuietSession(Center);
        session.Stats.AddEnhancement(ShipEnhancements.Regeneration);
        session.Stats.HitPoints = 50f;

        session.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(50.20f, session.Stats.HitPoints, precision: 2);

        for (var i = 0; i < 4; i++)
            session.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(51.00f, session.Stats.HitPoints, precision: 2);

        // Level 5 gives +1 per s.
        var lvl5 = TestEnemies.QuietSession(Center);
        var regen = lvl5.Stats.AddEnhancement(ShipEnhancements.Regeneration);
        while (regen.Level < 5)
            regen.RaiseLevel();
        lvl5.Stats.HitPoints = 50f;

        lvl5.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(51.00f, lvl5.Stats.HitPoints, precision: 2);

        // At 99.5/100, after 1 s HP is 100.
        lvl5.Stats.HitPoints = 99.5f;
        lvl5.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(100f, lvl5.Stats.HitPoints, precision: 2);

        // At level 20 (+4 per s) a hit changes nothing: the second after it heals what the second before it did.
        var hitSession = TestEnemies.QuietSession(Center);
        var hitRegen = hitSession.Stats.AddEnhancement(ShipEnhancements.Regeneration);
        while (hitRegen.Level < OwnedEnhancement.MaxLevel)
            hitRegen.RaiseLevel();
        hitSession.Stats.HitPoints = 50f;

        var beforeHit = hitSession.Stats.HitPoints;
        hitSession.Update(Vector2.Zero, null, 1f, Width, Height);
        var gainBeforeHit = hitSession.Stats.HitPoints - beforeHit;
        Assert.Equal(ShipEnhancements.RegenPerTick(20), gainBeforeHit, precision: 2);

        var toucher = EnemyRoster.Gemini with { HitPoints = 1_000_000f, ContactDamage = 10f };
        var enemy = TestEnemies.Stationary(toucher, Center);
        hitSession.AddEnemy(enemy);
        hitSession.Update(Vector2.Zero, null, Frame, Width, Height);
        enemy.TakeDamage(enemy.HitPoints);
        var hpAfterHit = hitSession.Stats.HitPoints;
        Assert.True(hpAfterHit < beforeHit + gainBeforeHit);

        hitSession.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(gainBeforeHit, hitSession.Stats.HitPoints - hpAfterHit, precision: 2);

        // Paused seconds heal nothing.
        var pausedSession = new GameSession(Center, new Random(1));
        Assert.True(pausedSession.IsChoosingUpgrade);
        pausedSession.Stats.AddEnhancement(ShipEnhancements.Regeneration);
        pausedSession.Stats.HitPoints = 50f;
        for (var i = 0; i < 5; i++)
            pausedSession.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(50f, pausedSession.Stats.HitPoints);

        // No healing after game over (0 HP)
        var deadSession = TestEnemies.QuietSession(Center);
        deadSession.Stats.AddEnhancement(ShipEnhancements.Regeneration);
        deadSession.Stats.HitPoints = 0f;
        deadSession.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(0f, deadSession.Stats.HitPoints);
    }

    private static (int, int, float, int, float, float, int) Snapshot(PlayerStats stats) =>
        (stats.Weapons.Count, stats.Laser().Level, stats.Laser().Damage, stats.Laser().Count, stats.Laser().AttacksPerSecond, stats.MaxHitPoints,
            stats.Enhancements.Sum(enhancement => enhancement.Level));

    private static (WeaponKind Kind, int Level, float Damage, float AttacksPerSecond, int Count, int Pierce, double Progress) WeaponSnapshot(OwnedWeapon weapon) =>
        (weapon.Kind, weapon.Level, weapon.Damage, weapon.AttacksPerSecond, weapon.Count, weapon.Pierce, weapon.TimerProgress);
}
