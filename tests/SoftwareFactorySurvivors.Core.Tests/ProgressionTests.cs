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
        int[] expected = [4, 10, 18, 28, 39];
        Assert.Equal(expected, Enumerable.Range(1, 5).Select(Leveling.Required));

        for (var level = 2; level < 30; level++)
        {
            var previousStep = Leveling.Required(level) - Leveling.Required(level - 1);
            var nextStep = Leveling.Required(level + 1) - Leveling.Required(level);
            Assert.True(nextStep >= previousStep, $"Level {level} cost grew more slowly than before");
        }
    }

    [Fact]
    public void Experience_carries_over_and_can_gain_several_levels_at_once()
    {
        var leveling = new Leveling();

        Assert.Equal(0, leveling.Add(3));
        Assert.Equal(1, leveling.Level);

        Assert.Equal(2, leveling.Add(4 + 10 - 3 + 3));
        Assert.Equal(3, leveling.Level);
        Assert.Equal(3, leveling.Experience);
        Assert.Equal(18, leveling.ExperienceToNextLevel);
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

        // Every option of every weapon has card text the vector font can draw.
        var all = new PlayerStats();
        foreach (var kind in Upgrades.AllWeapons)
            all.AddWeapon(kind);
        foreach (var card in Upgrades.Eligible(all))
        {
            var cardText = $"{Upgrades.Title(card, all)} {Upgrades.Describe(card, all)}";
            Assert.Contains(Upgrades.WeaponName(card.Weapon!.Value), cardText);
            Assert.Contains("LV 1 > 2", cardText);
            Assert.NotEmpty(VectorText.GetStrokes(cardText, Vector2.Zero, 10f));
        }
    }

    [Fact]
    public void Killing_enough_enemies_pauses_play_until_an_upgrade_is_chosen()
    {
        var session = TestEnemies.QuietSession(Center);
        // Gemini is worth 2 experience; level 2 needs 5, so the third kill levels up.
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
        // One kill worth 35 experience: 4 for level 2, 10 for level 3 and 18 for level 4, with 3 left over.
        var bigPrize = EnemyRoster.Meta with { Experience = 35 };
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

    [Fact]
    public void Extra_hit_points_survive_a_touch_with_brief_immunity()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 2;
        session.Stats.HitPoints = 2;
        session.AddEnemy(TestEnemies.StationaryGemini(Center));
        session.AddEnemy(TestEnemies.StationaryGemini(Center + new Vector2(0, 200)));

        session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.False(session.IsGameOver);
        Assert.Equal(1, session.Stats.HitPoints);
        Assert.Single(session.Enemies);
        Assert.Equal(GameSession.InvulnerableSeconds, session.InvulnerableSecondsLeft);

        // Fly the ship into the second enemy while still immune: no damage.
        session.AddEnemy(TestEnemies.StationaryGemini(Center));
        session.Update(Vector2.Zero, null, Frame, Width, Height);
        Assert.Equal(1, session.Stats.HitPoints);

        // Once immunity wears off, the next touch is fatal.
        for (var i = 0; i < 70 && !session.IsGameOver; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);
        Assert.True(session.IsGameOver);
        Assert.Equal(0, session.Stats.HitPoints);
    }

    [Fact]
    public void Enemy_types_join_the_spawner_at_their_arrival_times()
    {
        var session = new GameSession(Center, new Random(1)).WithLaser();
        Assert.Equal([EnemyRoster.Gemini], session.Spawner.Types);

        session.Spawner.SetSpawnsPerMinute(EnemyRoster.Gemini, 0f);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 1000;
        for (var t = 0; t < 31 && !session.IsFinished; t++)
        {
            session.Update(Vector2.Zero, null, 1f, Width, Height);
            while (session.IsChoosingUpgrade)
                session.ChooseUpgrade(0);
        }

        Assert.Contains(EnemyRoster.HuggingFace, session.Spawner.Types);
        Assert.DoesNotContain(EnemyRoster.ChatGpt, session.Spawner.Types);
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

    private static (int, int, float, int, float, int) Snapshot(PlayerStats stats) =>
        (stats.Weapons.Count, stats.Laser().Level, stats.Laser().Damage, stats.Laser().Count, stats.Laser().AttacksPerSecond, stats.MaxHitPoints);

    private static (WeaponKind Kind, int Level, float Damage, float AttacksPerSecond, int Count, int Pierce, double Progress) WeaponSnapshot(OwnedWeapon weapon) =>
        (weapon.Kind, weapon.Level, weapon.Damage, weapon.AttacksPerSecond, weapon.Count, weapon.Pierce, weapon.TimerProgress);
}
