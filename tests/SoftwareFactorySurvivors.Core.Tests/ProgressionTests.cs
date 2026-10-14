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
    public void Offer_is_three_different_upgrades_and_skips_maxed_ones()
    {
        var stats = new PlayerStats();

        for (var seed = 0; seed < 20; seed++)
        {
            var offer = Upgrades.Offer(stats, new Random(seed));
            Assert.Equal(3, offer.Count);
            Assert.Equal(3, offer.Distinct().Count());
        }

        stats.ProjectileCount = Upgrades.MaxProjectileCount;
        for (var seed = 0; seed < 20; seed++)
        {
            var offer = Upgrades.Offer(stats, new Random(seed));
            Assert.DoesNotContain(Upgrade.ProjectileCount, offer);
        }
    }

    [Fact]
    public void Each_upgrade_changes_its_attribute_and_describes_the_change()
    {
        var stats = new PlayerStats();

        Assert.Equal("1.00/S > 1.25/S", Upgrades.Describe(Upgrade.AttackSpeed, stats));
        Upgrades.Apply(Upgrade.AttackSpeed, stats);
        Assert.Equal(1.25f, stats.AttacksPerSecond);

        Assert.Equal("100% > 200%", Upgrades.Describe(Upgrade.ProjectileSize, stats));
        Upgrades.Apply(Upgrade.ProjectileSize, stats);
        Assert.Equal(2f, stats.ProjectileScale);

        Upgrades.Apply(Upgrade.ProjectileCount, stats);
        Assert.Equal(2, stats.ProjectileCount);

        Upgrades.Apply(Upgrade.Damage, stats);
        Assert.Equal(2f, stats.Damage);

        Upgrades.Apply(Upgrade.Piercing, stats);
        Assert.Equal(1, stats.Pierce);

        Assert.Equal("1/1 > 2/2", Upgrades.Describe(Upgrade.MaxHitPoints, stats));
        Upgrades.Apply(Upgrade.MaxHitPoints, stats);
        Assert.Equal((2, 2), (stats.HitPoints, stats.MaxHitPoints));

        foreach (var upgrade in Upgrades.All)
            Assert.NotEmpty(VectorText.GetStrokes(Upgrades.Title(upgrade) + " " + Upgrades.Describe(upgrade, stats), Vector2.Zero, 10f));
    }

    [Fact]
    public void Killing_enough_enemies_pauses_play_until_an_upgrade_is_chosen()
    {
        var session = TestEnemies.QuietSession(Center);
        // Gemini is worth 2 experience; level 2 needs 5, so the third kill levels up.
        for (var i = 0; i < 3; i++)
            session.AddEnemy(TestEnemies.StationaryGemini(Center + new Vector2(0, -120 - 60 * i)));
        session.Stats.Damage = 10f;
        session.Stats.Pierce = 5;

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
        // One kill worth 20 experience: 4 for level 2 plus 10 for level 3, with 6 left over.
        var bigPrize = EnemyRoster.Meta with { Experience = 20 };
        session.AddEnemy(TestEnemies.Stationary(bigPrize, Center + new Vector2(0, -150)));
        session.Stats.Damage = 100f;

        for (var i = 0; i < 60 * 2 && !session.IsChoosingUpgrade; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Equal(3, session.Leveling.Level);
        Assert.Equal(6, session.Leveling.Experience);
        session.ChooseUpgrade(1);
        Assert.True(session.IsChoosingUpgrade);
        session.ChooseUpgrade(2);
        Assert.False(session.IsChoosingUpgrade);
        Assert.Throws<InvalidOperationException>(() => session.ChooseUpgrade(0));
    }

    [Fact]
    public void Attack_speed_fires_volleys_more_often()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.AttacksPerSecond = 2.5f;
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
        session.Stats.ProjectileCount = 3;

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
        session.Stats.Pierce = 1;

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
        var session = new GameSession(Center, new Random(1));
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

    private static (float, int, float, int, int, float) Snapshot(PlayerStats stats) =>
        (stats.ProjectileScale, stats.ProjectileCount, stats.Damage, stats.Pierce, stats.MaxHitPoints, stats.AttacksPerSecond);
}
