using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>How level-up rounds are paced: their order, short offers, and levels with nothing left to offer.</summary>
public class PacingTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);
    private const float Frame = 1f / 60f;

    [Fact]
    public void Fewer_than_three_eligible_cards_offers_only_those()
    {
        // Every weapon has at least 3 options, so only a lone enhancement below level 5 leaves fewer than 3 cards.
        var bravo = new Enhancement("BRAVO", "B");
        var stats = MaxedWeapons();
        stats.AddEnhancement(bravo);
        for (var i = 0; i < 3; i++)
            Upgrades.Apply(Upgrade.ForEnhancement(bravo), stats);
        Assert.Equal(4, stats.Find(bravo)!.Level);

        var offer = Upgrades.Offer(stats, new Random(1), GameSession.UpgradeChoices, [bravo]);

        Assert.Equal([Upgrade.ForEnhancement(bravo)], offer);
    }

    [Fact]
    public void Levels_with_no_eligible_card_are_used_up_without_pausing_and_the_queue_keeps_draining()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.AddWeapon(WeaponKind.Lightning);
        session.Stats.AddWeapon(WeaponKind.Orbs);
        foreach (var weapon in session.Stats.Weapons)
        {
            while (weapon.Level < OwnedWeapon.MaxLevel)
                Upgrades.Apply(Upgrade.ForWeapon(weapon.Kind, Upgrades.OptionsFor(weapon.Kind)[0]), session.Stats);
        }
        foreach (var enhancement in Upgrades.Enhancements.Take(PlayerStats.MaxEnhancements))
        {
            Upgrades.Apply(Upgrade.NewEnhancement(enhancement), session.Stats);
            while (session.Stats.Find(enhancement)!.Level < OwnedEnhancement.MaxLevel)
                Upgrades.Apply(Upgrade.ForEnhancement(enhancement), session.Stats);
        }
        Assert.Empty(Upgrades.Eligible(session.Stats));
        session.Stats.Laser().Damage = 1000f;

        // One kill worth exactly two levels.
        var prize = EnemyRoster.ChatGpt with { Experience = Leveling.Required(1) + Leveling.Required(2) };
        session.AddEnemy(TestEnemies.Stationary(prize, session.Ship.Nose - new Vector2(0, 150)));
        for (var i = 0; i < 60 * 3 && session.Leveling.Level < 3; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Equal(3, session.Leveling.Level);
        Assert.False(session.IsChoosingUpgrade);
        Assert.Null(session.UpgradeOffer);
        var clock = session.ElapsedSeconds;
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.Equal(clock + 0.5f, session.ElapsedSeconds, precision: 4);
        Assert.Throws<InvalidOperationException>(() => session.ChooseUpgrade(0));
    }

    [Fact]
    public void An_out_of_range_choice_is_rejected_and_leaves_the_offer_in_place()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], rings: []);
        var offer = session.UpgradeOffer!;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ChooseUpgrade(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ChooseUpgrade(offer.Count));

        Assert.Same(offer, session.UpgradeOffer);
        Assert.True(session.IsStartingPick);
        Assert.Empty(session.Stats.Weapons);
    }

    [Fact]
    public void Starting_above_level_1_offers_the_pick_first_then_one_regular_round_per_skipped_level()
    {
        var session = new GameSession(Center, new Random(5), roster: [], bosses: [], rings: [], start: new StartOptions(StartLevel: 3));

        Assert.True(session.IsStartingPick);
        Assert.Equal(Upgrades.StartingWeapons, session.UpgradeOffer!);
        session.ChooseUpgrade(0);

        for (var round = 0; round < 2; round++)
        {
            Assert.True(session.IsChoosingUpgrade, $"Round {round + 1} wasn't offered");
            Assert.False(session.IsStartingPick);
            var eligible = Upgrades.Eligible(session.Stats);
            Assert.All(session.UpgradeOffer!, card => Assert.Contains(card, eligible));
            session.Update(Vector2.Zero, null, 1f, Width, Height);
            Assert.Equal(0f, session.ElapsedSeconds);
            session.ChooseUpgrade(0);
        }

        Assert.False(session.IsChoosingUpgrade);
        // The pick gave one weapon level and each of the two rounds exactly one more (weapon or enhancement).
        Assert.Equal(3, session.Stats.Weapons.Sum(weapon => weapon.Level) + session.Stats.Enhancements.Sum(enhancement => enhancement.Level));
        session.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(1f, session.ElapsedSeconds);
    }

    [Fact]
    public void Required_experience_matches_new_curve_is_below_old_curve_and_is_non_decreasing()
    {
        int[] expectedFirstTen = [2, 5, 8, 11, 15, 19, 23, 27, 31, 36];
        Assert.Equal(expectedFirstTen, Enumerable.Range(1, 10).Select(Leveling.Required));

        for (var n = 1; n <= 30; n++)
        {
            var oldFormula = (int)Math.Round(3.5 * Math.Pow(n, 1.5));
            Assert.True(Leveling.Required(n) < oldFormula, $"Level {n} should require less than old curve");
            Assert.True(Leveling.Required(n + 1) >= Leveling.Required(n), $"Level {n + 1} should be >= level {n}");
        }
    }

    [Fact]
    public void Session_run_past_90s_with_no_regular_spawns_receives_exactly_32_ring_geminis()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], start: new StartOptions(StartSeconds: 89.9f)).WithLaser();
        session.Stats.MaxHitPoints = 1_000_000f;
        session.Stats.HitPoints = 1_000_000f;

        Assert.Empty(session.Enemies);

        session.Update(Vector2.Zero, null, 0.2f, Width, Height);

        Assert.Equal(32, session.Enemies.Count);
        Assert.All(session.Enemies, enemy => Assert.Same(EnemyRoster.Gemini, enemy.Type));
    }

    [Fact]
    public void Regular_enemy_cap_holds_at_300_under_long_high_rate_run()
    {
        var fastType = EnemyRoster.Gemini with { Name = "FastSwarm", DefaultSpawnsPerMinute = 1200f };
        var session = new GameSession(Center, new Random(1), roster: [fastType], bosses: [], rings: []).WithLaser();
        session.Stats.MaxHitPoints = 1_000_000f;
        session.Stats.HitPoints = 1_000_000f;
        session.Stats.Laser().Damage = 0f;

        for (var i = 0; i < 30 * 60; i++)
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            Assert.True(session.LiveRegularEnemyCount <= GameSession.MaxRegularEnemies,
                $"Live count {session.LiveRegularEnemyCount} exceeded cap {GameSession.MaxRegularEnemies}");
        }

        Assert.Equal(GameSession.MaxRegularEnemies, session.LiveRegularEnemyCount);
    }

    [Fact]
    public void Spawning_resumes_below_cap_on_next_update_without_burst()
    {
        var type = EnemyRoster.Gemini with { Name = "Controlled", DefaultSpawnsPerMinute = 60f };
        var session = new GameSession(Center, new Random(1), roster: [type], bosses: [], rings: []).WithLaser();
        session.Stats.MaxHitPoints = 1_000_000f;
        session.Stats.HitPoints = 1_000_000f;

        for (var i = 0; i < GameSession.MaxRegularEnemies; i++)
            session.AddEnemy(TestEnemies.StationaryGemini(new Vector2(-500, -500)));

        Assert.Equal(GameSession.MaxRegularEnemies, session.LiveRegularEnemyCount);

        // Advance 10 seconds while at cap. The spawner should be frozen, not accumulating.
        session.Update(Vector2.Zero, null, 10f, Width, Height);
        Assert.Equal(GameSession.MaxRegularEnemies, session.LiveRegularEnemyCount);

        // Kill 10 enemies so live regular count is 290
        for (var i = 0; i < 10; i++)
            session.Enemies[i].TakeDamage(100f);

        // Advance 1 frame to clear dead enemies and tick
        session.Update(Vector2.Zero, null, Frame, Width, Height);
        Assert.Equal(290, session.LiveRegularEnemyCount);

        // Advance 1 second. At 60/min = 1/s, it should spawn exactly 1 enemy, not an accumulated burst
        session.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(291, session.LiveRegularEnemyCount);
    }

    [Fact]
    public void Scheduled_rings_are_exempt_from_the_300_regular_enemy_cap()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [],
            rings: [new RingSpawn(10f, EnemyRoster.Gemini, 32, 500f)],
            start: new StartOptions(StartSeconds: 9.9f)).WithLaser();
        session.Stats.MaxHitPoints = 1_000_000f;
        session.Stats.HitPoints = 1_000_000f;

        for (var i = 0; i < GameSession.MaxRegularEnemies; i++)
            session.AddEnemy(TestEnemies.StationaryGemini(new Vector2(-500, -500)));

        Assert.Equal(GameSession.MaxRegularEnemies, session.LiveRegularEnemyCount);

        // Advance past 10s: ring arrives despite being at cap
        session.Update(Vector2.Zero, null, 0.2f, Width, Height);

        Assert.Equal(GameSession.MaxRegularEnemies + 32, session.LiveRegularEnemyCount);
    }

    [Fact]
    public void Combined_regular_spawn_rates_step_up_with_each_boss_and_boss_rates_are_zero()
    {
        float SumArrivedBy(float seconds) =>
            EnemyRoster.All.Where(type => type.ArrivesAtSeconds <= seconds).Sum(type => type.DefaultSpawnsPerMinute);

        // Until the first boss at 3:00: Gemini alone.
        Assert.Equal(160f, SumArrivedBy(0f));
        Assert.Equal(160f, SumArrivedBy(179.9f));

        // Each of the first three bosses brings one more type, and earlier types stay.
        Assert.Equal(160f + 240f, SumArrivedBy(180f));
        Assert.Equal(160f + 240f + 50f, SumArrivedBy(360f));
        Assert.Equal(160f + 240f + 50f + 20f, SumArrivedBy(540f));
        Assert.Equal(470f, SumArrivedBy(720f));

        // All boss types keep rate of 0
        Assert.All(EnemyRoster.Bosses, arrival => Assert.Equal(0f, arrival.Type.DefaultSpawnsPerMinute));
    }

    [Fact]
    public void Boss_arrives_on_schedule_while_regular_enemy_count_is_at_cap()
    {
        var session = new GameSession(Center, new Random(1), roster: [], rings: [],
            start: new StartOptions(StartSeconds: 179.9f)).WithLaser();
        session.Stats.MaxHitPoints = 1_000_000f;
        session.Stats.HitPoints = 1_000_000f;

        for (var i = 0; i < GameSession.MaxRegularEnemies; i++)
            session.AddEnemy(TestEnemies.StationaryGemini(new Vector2(-500, -500)));

        Assert.Equal(GameSession.MaxRegularEnemies, session.LiveRegularEnemyCount);
        Assert.False(session.IsBossActive);

        // Advance 0.2s past 180s
        session.Update(Vector2.Zero, null, 0.2f, Width, Height);

        Assert.True(session.IsBossActive);
        Assert.Single(session.Bosses);
        Assert.Same(EnemyRoster.GeminiBoss, session.Bosses[0].Type);
        Assert.Contains(session.Bosses[0], session.Enemies);
    }

    [Fact]
    public void Boss_thrown_minions_are_added_in_full_while_regular_enemy_count_is_at_cap()
    {
        var session = new GameSession(Center, new Random(1), roster: [], rings: [],
            start: new StartOptions(StartSeconds: 179.9f)).WithLaser();
        session.Stats.MaxHitPoints = 1_000_000f;
        session.Stats.HitPoints = 1_000_000f;
        session.Stats.Laser().Damage = 0f;

        for (var i = 0; i < GameSession.MaxRegularEnemies; i++)
            session.AddEnemy(TestEnemies.StationaryGemini(new Vector2(-500, -500)));
        Assert.Equal(GameSession.MaxRegularEnemies, session.LiveRegularEnemyCount);
        var seeded = session.Enemies.ToHashSet();

        // The boss arrives, then throws a wave of minions every 3 seconds; none is held back by the cap.
        for (var i = 0; i < 60 * 4; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.True(session.IsBossActive);
        var boss = Assert.Single(session.Bosses);
        // The boss throws one Gemini at the 3 s mark; it is in play although the regular count was already at the cap.
        var thrown = Assert.Single(session.Enemies, enemy => enemy.Movement is Thrown);
        Assert.Same(EnemyRoster.Gemini, thrown.Type);
        Assert.DoesNotContain(thrown, seeded);
        Assert.True(session.LiveRegularEnemyCount > GameSession.MaxRegularEnemies);
        Assert.Contains(boss, session.Enemies);
    }

    /// <summary>Stats owning three weapons (the first three kinds), each at level 20.</summary>
    private static PlayerStats MaxedWeapons()
    {
        var stats = new PlayerStats();
        foreach (var kind in Upgrades.AllWeapons.Take(PlayerStats.MaxWeapons))
        {
            var weapon = stats.AddWeapon(kind);
            while (weapon.Level < OwnedWeapon.MaxLevel)
                Upgrades.Apply(Upgrade.ForWeapon(kind, Upgrades.OptionsFor(kind)[0]), stats);
        }
        return stats;
    }
}
