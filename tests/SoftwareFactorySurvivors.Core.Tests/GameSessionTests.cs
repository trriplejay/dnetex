using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class GameSessionTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void Contact_damage_taking_hit_points_to_zero_ends_the_game_as_a_loss_and_freezes_it()
    {
        var session = new GameSession(Center, new Random(1)).WithLaser();
        var tick = EnemyRoster.ScaledContactDamage(EnemyRoster.Gemini.ContactDamage, 0.1f);
        session.Stats.HitPoints = tick - 1f;
        session.AddEnemy(EnemyRoster.Gemini.Create(Center + new Vector2(60, 0)));

        session.Update(Vector2.Zero, null, 0.1f, Width, Height);
        Assert.False(session.IsGameOver);

        for (var i = 0; i < 20 && !session.IsGameOver; i++)
            session.Update(Vector2.Zero, null, 0.05f, Width, Height);
        Assert.True(session.IsGameOver);
        Assert.False(session.IsVictory);
        Assert.Equal(0f, session.Stats.HitPoints);

        var shipPosition = session.Ship.Position;
        var enemyPosition = session.Enemies[0].Position;
        var elapsed = session.ElapsedSeconds;
        for (var i = 0; i < 3; i++)
            session.Update(new Vector2(0, -1), null, 1f, Width, Height);
        Assert.Equal(shipPosition, session.Ship.Position);
        Assert.Equal(enemyPosition, session.Enemies[0].Position);
        Assert.Equal(elapsed, session.ElapsedSeconds);
        Assert.Equal(0f, session.Stats.HitPoints);
    }

    [Fact]
    public void A_ship_with_more_than_one_tick_left_survives_a_touch()
    {
        var session = TestEnemies.QuietSession(Center);
        var tick = EnemyRoster.ScaledContactDamage(EnemyRoster.Gemini.ContactDamage, 0.01f);
        session.Stats.HitPoints = tick + 1f;
        session.AddEnemy(TestEnemies.StationaryGemini(Center));

        session.Update(Vector2.Zero, null, 0.01f, Width, Height);

        Assert.False(session.IsGameOver);
        Assert.Equal(1f, session.Stats.HitPoints, precision: 2);
    }

    [Fact]
    public void Enemy_in_the_concave_gap_beside_the_ship_is_not_contact()
    {
        var session = new GameSession(Center, new Random(1)).WithLaser();
        var enemy = TestEnemies.StationaryGemini(Center + new Vector2(50, -14));
        session.AddEnemy(enemy);

        session.Update(Vector2.Zero, null, 0.01f, Width, Height);

        Assert.False(session.IsGameOver);
    }

    [Fact]
    public void Lasers_damage_the_enemy_they_hit_and_are_used_up()
    {
        var session = TestEnemies.QuietSession(Center);
        var enemy = TestEnemies.StationaryGemini(Center + new Vector2(0, -150));
        session.AddEnemy(enemy);

        // One laser per second flies up from the nose into the enemy.
        for (var i = 0; i < 90 && enemy.HitPoints == 3; i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        Assert.Equal(2f, enemy.HitPoints);
        Assert.Empty(session.Lasers);
        Assert.False(session.IsGameOver);
    }

    [Fact]
    public void Enemy_with_no_hit_points_left_is_removed()
    {
        var session = TestEnemies.QuietSession(Center);
        var enemy = TestEnemies.StationaryGemini(Center + new Vector2(0, -150));
        session.AddEnemy(enemy);

        for (var i = 0; i < 60 * 5 && session.Enemies.Contains(enemy); i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        Assert.True(enemy.IsDead);
        Assert.DoesNotContain(enemy, session.Enemies);
    }

    [Fact]
    public void Lasers_fire_toward_the_aim_point_while_moving_in_another_direction()
    {
        var session = TestEnemies.QuietSession(Center);
        var target = TestEnemies.StationaryGemini(Center + new Vector2(300, 100));
        session.AddEnemy(target);

        // Fly up while aiming at the enemy down and to the right; aim follows it every frame.
        for (var i = 0; i < 150 && target.HitPoints == 3f; i++)
            session.Update(new Vector2(0, -1), target.Position, 1f / 60f, Width, Height);

        Assert.Equal(2f, target.HitPoints);
        Assert.True(session.Ship.Position.Y < Center.Y);
    }
    [Fact]
    public void Level_3_round_offers_regular_upgrade_cards()
    {
        var session = TestEnemies.QuietSession(Center);
        KillFor(session, Leveling.Required(1));
        session.ChooseUpgrade(0);

        KillFor(session, Leveling.Required(2));

        Assert.Equal(3, session.Leveling.Level);
        var offer = session.UpgradeOffer!;
        Assert.Equal(GameSession.UpgradeChoices, offer.Count);
        var eligible = Upgrades.Eligible(session.Stats);
        Assert.All(offer, upgrade => Assert.Contains(upgrade, eligible));
    }

    [Fact]
    public void A_new_session_opens_on_the_starting_pick_whatever_the_seed_and_gives_only_the_picked_weapon()
    {
        Upgrade[] expected =
        [
            Upgrade.NewWeapon(WeaponKind.Laser),
            Upgrade.NewWeapon(WeaponKind.Lightning),
            Upgrade.NewWeapon(WeaponKind.Orbs),
            Upgrade.NewWeapon(WeaponKind.RicochetDisc),
            Upgrade.NewWeapon(WeaponKind.ProximityMines),
        ];
        var first = new GameSession(Center, new Random(1));
        var second = new GameSession(Center, new Random(987_654));

        foreach (var session in new[] { first, second })
        {
            Assert.True(session.IsChoosingUpgrade);
            Assert.True(session.IsStartingPick);
            Assert.Equal(expected, session.UpgradeOffer!);
            Assert.Empty(session.Stats.Weapons);
            session.Update(new Vector2(0, -1), null, 1f, Width, Height);
            Assert.Equal(0f, session.ElapsedSeconds);
            Assert.Equal(Center, session.Ship.Position);
        }

        first.ChooseUpgrade(1);

        var owned = Assert.Single(first.Stats.Weapons);
        Assert.Equal((WeaponKind.Lightning, 1), (owned.Kind, owned.Level));
        Assert.False(first.IsChoosingUpgrade);
        Assert.False(first.IsStartingPick);
        first.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(1f, first.ElapsedSeconds);
    }

    [Fact]
    public void The_starting_pick_lists_all_five_weapons_in_order_and_picking_disc_or_mines_gives_only_that_weapon()
    {
        Upgrade[] expected =
        [
            Upgrade.NewWeapon(WeaponKind.Laser),
            Upgrade.NewWeapon(WeaponKind.Lightning),
            Upgrade.NewWeapon(WeaponKind.Orbs),
            Upgrade.NewWeapon(WeaponKind.RicochetDisc),
            Upgrade.NewWeapon(WeaponKind.ProximityMines),
        ];
        foreach (var (index, kind) in new[] { (3, WeaponKind.RicochetDisc), (4, WeaponKind.ProximityMines) })
        {
            var session = new GameSession(Center, new Random(index * 31), roster: [], bosses: [], rings: []);
            Assert.True(session.IsChoosingUpgrade);
            Assert.Equal(expected, session.UpgradeOffer!);

            session.ChooseUpgrade(index);

            var owned = Assert.Single(session.Stats.Weapons);
            Assert.Equal((kind, 1), (owned.Kind, owned.Level));
            Assert.False(session.IsChoosingUpgrade);
            Assert.False(session.IsStartingPick);
        }
    }

    [Fact]
    public void Picking_orbs_at_the_start_gives_an_orbs_weapon_whose_options_change_its_orbs()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], rings: []);
        session.ChooseUpgrade(2);
        var orbs = Assert.Single(session.Stats.Weapons);
        Assert.Equal(WeaponKind.Orbs, orbs.Kind);

        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Orbs, WeaponOption.OrbsCount), session.Stats);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Orbs, WeaponOption.OrbsSpeedAndOrbit), session.Stats);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Orbs, WeaponOption.OrbsRadius), session.Stats);

        var state = orbs.Orbs!;
        Assert.Equal(4, orbs.Level);
        Assert.Equal(4, state.Level);
        Assert.Equal(4, state.OrbCount);
        Assert.Equal(4, orbs.Count);
        Assert.Equal(120f, state.RotationSpeedDeg);
        Assert.Equal(95f, state.OrbitRadius);
        Assert.Equal(11f, state.OrbRadius);
        Assert.False(state.IsRingMode);

        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Orbs, WeaponOption.OrbsDamage), session.Stats);
        Assert.Equal(1.5f, state.Damage);
        Assert.Equal(1.5f, orbs.Damage);
        Assert.True(state.IsRingMode);

        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Orbs, WeaponOption.OrbsDamage), session.Stats);
        Assert.Equal(2.0f, state.Damage);
        Assert.Equal(2.0f, orbs.Damage);
        Assert.Equal(6, orbs.Level);
        Assert.Equal(6, state.Level);
    }

    [Fact]
    public void Each_weapon_option_changes_only_its_own_stat_and_raises_one_level()
    {
        var stats = new PlayerStats();
        var laser = stats.AddWeapon(WeaponKind.Laser);
        var lightning = stats.AddWeapon(WeaponKind.Lightning);

        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningDamage), stats);
        Assert.Equal((2, 1.2f, 1.2f, 1), (lightning.Level, lightning.Damage, lightning.AttacksPerSecond, lightning.Count));
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningAttackSpeed), stats);
        Assert.Equal((3, 1.2f, 1), (lightning.Level, lightning.Damage, lightning.Count));
        Assert.Equal(1.4f, lightning.AttacksPerSecond, precision: 5);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), stats);
        Assert.Equal((4, 1.2f, 2), (lightning.Level, lightning.Damage, lightning.Count));
        Assert.Equal(1.4f, lightning.AttacksPerSecond, precision: 5);

        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserProjectile), stats);
        Assert.Equal((2, 1f, 1f, 2), (laser.Level, laser.Damage, laser.AttacksPerSecond, laser.Count));
        Assert.Equal(4, lightning.Level);

        // A card for a weapon the ship doesn't own, or an option from another weapon's list, is refused.
        Assert.Throws<InvalidOperationException>(() =>
            Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Orbs, WeaponOption.OrbsDamage), stats));
        Assert.Throws<ArgumentException>(() => Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LightningChain));
    }

    [Fact]
    public void Offers_hold_new_weapon_cards_only_while_a_slot_is_free_and_never_raise_a_weapon_past_level_5()
    {
        var sawNewWeapon = false;
        for (var seed = 0; seed < 200; seed++)
        {
            // One weapon owned: new-weapon cards for the four others (and new-enhancement cards) may appear; every card is eligible.
            var one = new PlayerStats();
            one.AddWeapon(WeaponKind.Laser);
            var offer = Upgrades.Offer(one, new Random(seed), GameSession.UpgradeChoices);
            Assert.Equal(GameSession.UpgradeChoices, offer.Count);
            Assert.Equal(offer.Count, offer.Distinct().Count());
            Assert.All(offer, card => Assert.True(
                card.Kind == UpgradeKind.WeaponUpgrade && card.Weapon == WeaponKind.Laser
                || card.Kind == UpgradeKind.NewWeapon && card.Weapon != WeaponKind.Laser
                || card.Kind == UpgradeKind.NewEnhancement,
                $"Ineligible card {card}"));
            sawNewWeapon |= offer.Any(card => card.Kind == UpgradeKind.NewWeapon);

            // Three weapons owned, the laser at level 20: no new-weapon card and no laser card.
            var full = new PlayerStats();
            var laser = full.AddWeapon(WeaponKind.Laser);
            full.AddWeapon(WeaponKind.Lightning);
            full.AddWeapon(WeaponKind.Orbs);
            for (var i = 0; i < 19; i++)
                Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserDamage), full);
            Assert.Equal(OwnedWeapon.MaxLevel, laser.Level);
            var random = new Random(seed);
            // Weapon cards only here (no enhancement list), so every pick below names a weapon.
            var fullOffer = Upgrades.Offer(full, random, GameSession.UpgradeChoices, enhancements: []);
            Assert.Equal(GameSession.UpgradeChoices, fullOffer.Distinct().Count());
            Assert.DoesNotContain(fullOffer, card => card.Kind == UpgradeKind.NewWeapon);
            Assert.DoesNotContain(fullOffer, card => card.Weapon == WeaponKind.Laser);

            // Taking cards one at a time raises the picked weapon exactly one level and never past 5.
            var pick = fullOffer[seed % fullOffer.Count];
            var target = full.Find(pick.Weapon!.Value)!;
            var before = target.Level;
            Upgrades.Apply(pick, full);
            Assert.Equal(before + 1, target.Level);
            for (var round = 0; round < 20; round++)
            {
                var next = Upgrades.Offer(full, random, GameSession.UpgradeChoices, enhancements: []);
                if (next.Count == 0)
                    break;
                Upgrades.Apply(next[0], full);
                Assert.All(full.Weapons, weapon => Assert.InRange(weapon.Level, 1, OwnedWeapon.MaxLevel));
            }
        }
        Assert.True(sawNewWeapon, "No offer included a new-weapon card");
    }

    [Fact]
    public void Enhancement_cards_follow_the_three_slot_rule_and_the_level_5_cap()
    {
        Enhancement[] stubs = [new("ALPHA", "A"), new("BRAVO", "B"), new("CHARLIE", "C"), new("DELTA", "D")];
        var stats = new PlayerStats();
        stats.AddWeapon(WeaponKind.Laser);

        // Slots free: every stub can be offered as a new enhancement.
        var open = Upgrades.Eligible(stats, stubs);
        Assert.Equal(stubs.Select(Upgrade.NewEnhancement), open.Where(card => card.Kind == UpgradeKind.NewEnhancement));

        Upgrades.Apply(Upgrade.NewEnhancement(stubs[0]), stats);
        Upgrades.Apply(Upgrade.NewEnhancement(stubs[1]), stats);
        Upgrades.Apply(Upgrade.NewEnhancement(stubs[2]), stats);
        var alpha = stats.Find(stubs[0])!;
        for (var i = 0; i < 19; i++)
            Upgrades.Apply(Upgrade.ForEnhancement(stubs[0]), stats);
        Assert.Equal(OwnedEnhancement.MaxLevel, alpha.Level);

        for (var seed = 0; seed < 200; seed++)
        {
            var offer = Upgrades.Offer(stats, new Random(seed), GameSession.UpgradeChoices, stubs);
            Assert.DoesNotContain(Upgrade.NewEnhancement(stubs[3]), offer);
            Assert.DoesNotContain(Upgrade.ForEnhancement(stubs[0]), offer);
        }
        var eligible = Upgrades.Eligible(stats, stubs);
        Assert.DoesNotContain(eligible, card => card.Kind == UpgradeKind.NewEnhancement);
        Assert.Equal([Upgrade.ForEnhancement(stubs[1]), Upgrade.ForEnhancement(stubs[2])],
            eligible.Where(card => card.Kind == UpgradeKind.EnhancementUpgrade));
        Assert.Throws<InvalidOperationException>(() => Upgrades.Apply(Upgrade.NewEnhancement(stubs[3]), stats));
        Assert.Throws<InvalidOperationException>(() => Upgrades.Apply(Upgrade.ForEnhancement(stubs[0]), stats));

        // The production list is the ship's five; an offer only ever holds weapon cards and those five.
        Assert.Equal(ShipEnhancements.All, Upgrades.Enhancements);
        for (var seed = 0; seed < 200; seed++)
        {
            var fresh = new PlayerStats();
            fresh.AddWeapon(WeaponKind.Laser);
            Assert.All(Upgrades.Offer(fresh, new Random(seed), GameSession.UpgradeChoices),
                card => Assert.True(card.Kind is UpgradeKind.NewWeapon or UpgradeKind.WeaponUpgrade
                    || ShipEnhancements.All.Contains(card.Enhancement!)));
        }
    }

    public enum DiscOrMineSource
    {
        Disc,
        Shard,
        Mine,
        MiniMine,
    }

    [Theory]
    [InlineData(DiscOrMineSource.Disc)]
    [InlineData(DiscOrMineSource.Shard)]
    [InlineData(DiscOrMineSource.Mine)]
    [InlineData(DiscOrMineSource.MiniMine)]
    public void A_kill_by_disc_shard_mine_or_mini_mine_credits_score_and_experience_exactly_once(DiscOrMineSource source)
    {
        var (session, victim) = ArrangeDiscOrMineKill(source);
        var sawShard = false;

        for (var i = 0; i < 6 * 60 && !victim.IsDead; i++)
        {
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
            sawShard |= session.DiscShards.Count > 0;
        }

        Assert.True(victim.IsDead);
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(victim.Experience, session.Leveling.Experience);
        if (source == DiscOrMineSource.Shard)
            Assert.True(sawShard, "The victim should die only once the disc has shattered");

        // The weapon keeps working; the kill is never credited again.
        for (var i = 0; i < 3 * 60; i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(victim.Experience, session.Leveling.Experience);
    }

    [Fact]
    public void Two_mini_mine_blasts_overlapping_a_dying_enemy_credit_its_kill_once()
    {
        var session = KillCreditTests.SessionWith(WeaponKind.ProximityMines);
        // Level 5: blast 90 px (mini-mines 45 px), damage 6 (mini-mines 3).
        RaiseWeapon(session, WeaponKind.ProximityMines, WeaponOption.MinesBlastRadius, 2);
        RaiseWeapon(session, WeaponKind.ProximityMines, WeaponOption.MinesDamage, 2);
        Assert.True(session.Stats.Find(WeaponKind.ProximityMines)!.IsFinale);
        session.AddEnemy(TestEnemies.Stationary(EnemyRoster.HuggingFace with { HitPoints = 1000f, Experience = 3 }, Center));
        // Between the first two mini-mines (thrown at 0 and 120 degrees), 43.3 px from each: inside both their
        // blasts. The mine's blast leaves it 1 HP, then the two mini-mines go off on the same frame.
        var spot = Center + 25f * new Vector2(MathF.Cos(MathF.PI / 3f), MathF.Sin(MathF.PI / 3f));
        var victim = TestEnemies.Stationary(EnemyRoster.HuggingFace with { HitPoints = 7f, Experience = 1 }, spot);
        session.AddEnemy(victim);

        IReadOnlyList<Circle> blastsThatKilled = [];
        for (var i = 0; i < 6 * 60 && !victim.IsDead; i++)
        {
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
            blastsThatKilled = session.MineExplosions.ToArray();
        }

        Assert.True(victim.IsDead);
        Assert.Equal(2, blastsThatKilled.Count(blast => Vector2.Distance(blast.Center, spot) <= blast.Radius));
        Assert.All(blastsThatKilled, blast => Assert.Equal(45f, blast.Radius));
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(victim.Experience, session.Leveling.Experience);
    }

    private static (GameSession Session, Enemy Victim) ArrangeDiscOrMineKill(DiscOrMineSource source)
    {
        var victimType = EnemyRoster.HuggingFace with { HitPoints = 1f, Experience = 1 };
        var tough = EnemyRoster.HuggingFace with { HitPoints = 1000f, Experience = 3 };
        switch (source)
        {
            case DiscOrMineSource.Disc:
            {
                var session = KillCreditTests.SessionWith(WeaponKind.RicochetDisc);
                var victim = TestEnemies.Stationary(victimType, Center + new Vector2(0, -150));
                session.AddEnemy(victim);
                return (session, victim);
            }
            case DiscOrMineSource.Shard:
            {
                var session = KillCreditTests.SessionWith(WeaponKind.RicochetDisc);
                RaiseWeapon(session, WeaponKind.RicochetDisc, WeaponOption.DiscDamage, 19);
                // Beyond the disc's 250 px reach, within a shard's 200 px flight from the far point.
                var victim = TestEnemies.Stationary(victimType, Center + new Vector2(0, -310));
                session.AddEnemy(victim);
                return (session, victim);
            }
            case DiscOrMineSource.Mine:
            {
                var session = KillCreditTests.SessionWith(WeaponKind.ProximityMines);
                var victim = TestEnemies.Stationary(victimType, Center);
                session.AddEnemy(victim);
                return (session, victim);
            }
            case DiscOrMineSource.MiniMine:
            {
                var session = KillCreditTests.SessionWith(WeaponKind.ProximityMines);
                RaiseWeapon(session, WeaponKind.ProximityMines, WeaponOption.MinesDamage, 19);
                session.AddEnemy(TestEnemies.Stationary(tough, Center));
                // Outside the mine's 60 px blast, inside the 30 px blast of the mini-mine thrown 50 px to the right.
                var victim = TestEnemies.Stationary(victimType, Center + new Vector2(75, 0));
                session.AddEnemy(victim);
                return (session, victim);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    private static void RaiseWeapon(GameSession session, WeaponKind kind, WeaponOption option, int times)
    {
        for (var i = 0; i < times; i++)
            Upgrades.Apply(Upgrade.ForWeapon(kind, option), session.Stats);
    }

    /// <summary>Puts a stationary enemy worth the given experience in front of the ship and shoots it until a level-up offer appears.</summary>
    private static void KillFor(GameSession session, int experience, List<Projectile>? fired = null)
    {
        session.Stats.Laser().Damage = 1000f;
        var prize = EnemyRoster.Meta with { Experience = experience };
        session.AddEnemy(TestEnemies.Stationary(prize, session.Ship.Nose - new Vector2(0, 150)));
        for (var i = 0; i < 60 * 3 && !session.IsChoosingUpgrade; i++)
        {
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
            fired?.AddRange(session.Lasers);
        }
        Assert.True(session.IsChoosingUpgrade, "No level-up offer appeared");
    }

    [Fact]
    public void When_all_three_weapons_and_three_enhancements_are_maxed_at_level_20_pending_level_up_is_consumed_with_no_pause()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.AddWeapon(WeaponKind.Lightning);
        session.Stats.AddWeapon(WeaponKind.Orbs);

        foreach (var weapon in session.Stats.Weapons)
        {
            var options = Upgrades.OptionsFor(weapon.Kind);
            for (var i = 0; i < 19; i++)
                Upgrades.Apply(Upgrade.ForWeapon(weapon.Kind, options[0]), session.Stats);
            Assert.Equal(20, weapon.Level);
        }

        var enhancementsToMax = Upgrades.Enhancements.Take(PlayerStats.MaxEnhancements).ToArray();
        foreach (var enhancement in enhancementsToMax)
        {
            Upgrades.Apply(Upgrade.NewEnhancement(enhancement), session.Stats);
            for (var i = 0; i < 19; i++)
                Upgrades.Apply(Upgrade.ForEnhancement(enhancement), session.Stats);
            Assert.Equal(20, session.Stats.Find(enhancement)!.Level);
        }

        Assert.Equal(3, session.Stats.Weapons.Count);
        Assert.Equal(3, session.Stats.Enhancements.Count);
        Assert.All(session.Stats.Weapons, w => Assert.Equal(20, w.Level));
        Assert.All(session.Stats.Enhancements, e => Assert.Equal(20, e.Level));
        Assert.Empty(Upgrades.Eligible(session.Stats));

        session.Stats.Laser().Damage = 1000f;
        var prize = EnemyRoster.Meta with { Experience = Leveling.Required(1) };
        session.AddEnemy(TestEnemies.Stationary(prize, session.Ship.Nose - new Vector2(0, 150)));

        for (var i = 0; i < 60 * 3 && session.Leveling.Level < 2; i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        Assert.Equal(2, session.Leveling.Level);
        Assert.False(session.IsChoosingUpgrade);
        Assert.Null(session.UpgradeOffer);

        var clockBefore = session.ElapsedSeconds;
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.Equal(clockBefore + 0.5f, session.ElapsedSeconds, precision: 4);
    }
}
