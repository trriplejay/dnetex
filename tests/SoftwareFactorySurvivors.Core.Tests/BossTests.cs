using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class BossTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);
    private const float Frame = 1f / 60f;

    [Fact]
    public void Gemini_boss_is_a_giant_slow_gemini_with_40_hit_points_arriving_at_two_minutes()
    {
        var boss = EnemyRoster.GeminiBoss;

        Assert.Equal(40f, boss.HitPoints);
        Assert.Equal(4f * EnemyRoster.Gemini.Radius, boss.Radius);
        Assert.True(boss.Speed <= EnemyRoster.Gemini.Speed / 2f);
        Assert.Equal(EnemyRoster.Gemini.Outline[0].Length, boss.Outline[0].Length);
        Assert.All(boss.Outline[0].Zip(EnemyRoster.Gemini.Outline[0]), pair =>
            Assert.True(Vector2.Distance(pair.First, pair.Second * 4f) < 0.01f));
        Assert.Equal([new BossArrival(120f, boss)], EnemyRoster.Bosses);
        Assert.DoesNotContain(boss, EnemyRoster.All);
    }

    [Fact]
    public void Thrown_enemy_flies_to_its_spot_at_its_flight_speed_then_moves_normally()
    {
        var spot = new Vector2(400, 0);
        var thrown = new Thrown(spot, 360f, new ChasePlayer(90f));
        var player = new Vector2(400, 1000);

        // The flight ignores the player and heads straight for the spot.
        var position = thrown.Move(Vector2.Zero, player, 0.5f);
        Assert.Equal(new Vector2(180, 0), position);
        Assert.False(thrown.HasLanded);

        position = thrown.Move(position, player, 1f);
        Assert.Equal(spot, position);
        Assert.True(thrown.HasLanded);

        // After landing it chases at its regular speed.
        position = thrown.Move(position, player, 1f);
        Assert.Equal(spot + new Vector2(0, 90), position);
    }

    [Fact]
    public void Boss_throws_a_regular_gemini_at_4x_speed_to_a_spot_near_the_player()
    {
        var player = new Vector2(500, 400);
        var boss = new Enemy(EnemyRoster.GeminiBoss, new Vector2(100, 100));

        Assert.Empty(boss.UseAbility(player, 2.9f, new Random(1)));
        var minion = Assert.Single(boss.UseAbility(player, 0.1f, new Random(1)));

        Assert.Same(EnemyRoster.Gemini, minion.Type);
        Assert.Equal(EnemyRoster.Gemini.HitPoints, minion.HitPoints);
        Assert.Equal(boss.Position, minion.Position);
        var flight = Assert.IsType<Thrown>(minion.Movement);
        Assert.InRange(Vector2.Distance(flight.LandingSpot, player), 80f, 180f);

        var start = minion.Position;
        minion.Update(player, 0.1f);
        Assert.Equal(4f * EnemyRoster.Gemini.Speed * 0.1f, Vector2.Distance(start, minion.Position), precision: 2);
    }

    [Fact]
    public void Landing_spots_vary_from_throw_to_throw()
    {
        var player = new Vector2(500, 400);
        var boss = new Enemy(EnemyRoster.GeminiBoss, new Vector2(100, 100));
        var random = new Random(5);

        var spots = Enumerable.Range(0, 5)
            .Select(_ => ((Thrown)Assert.Single(boss.UseAbility(player, 3f, random)).Movement).LandingSpot)
            .ToArray();

        Assert.Equal(5, spots.Distinct().Count());
    }

    [Fact]
    public void Boss_enters_at_two_minutes_and_pauses_regular_spawning_until_defeated()
    {
        var session = new GameSession(Center, new Random(2), roster: [EnemyRoster.Gemini]);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 1_000_000;
        var seen = new HashSet<Enemy>();

        RunUntil(session, () => session.ElapsedSeconds >= 119.9f);
        Assert.False(session.IsBossActive);

        RunUntil(session, () => session.IsBossActive);
        Assert.InRange(session.ElapsedSeconds, 120f, 120.1f);
        var boss = Assert.Single(session.Bosses);
        Assert.Same(EnemyRoster.GeminiBoss, boss.Type);
        Assert.Contains(boss, session.Enemies);
        seen.UnionWith(session.Enemies);

        // During the fight every new enemy is one the boss threw (one every 3 seconds); nothing regular spawns.
        var duringFight = new List<Enemy>();
        for (var i = 0; i < 60 * 10; i++)
        {
            Step(session);
            duringFight.AddRange(session.Enemies.Where(seen.Add));
        }
        Assert.Equal(3, duringFight.Count);
        Assert.All(duringFight, enemy => Assert.IsType<Thrown>(enemy.Movement));

        boss.TakeDamage(boss.HitPoints);
        Step(session);
        Assert.False(session.IsBossActive);
        Assert.DoesNotContain(boss, session.Enemies);
        seen.UnionWith(session.Enemies);

        // Spawning resumes at the regular rate (a Gemini every 2 seconds) with no saved-up burst.
        var afterFight = new List<Enemy>();
        for (var i = 0; i < 60 * 4; i++)
        {
            Step(session);
            afterFight.AddRange(session.Enemies.Where(seen.Add));
        }
        Assert.Equal(2, afterFight.Count);
        Assert.All(afterFight, enemy => Assert.IsType<ChasePlayer>(enemy.Movement));
    }

    [Fact]
    public void Boss_survives_touching_the_ship_when_the_ship_survives()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [new BossArrival(0f, EnemyRoster.GeminiBoss)]);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 3;

        Step(session);
        var boss = Assert.Single(session.Bosses);

        // Drive the ship into the boss.
        RunUntil(session, () => session.Stats.HitPoints < 3, toward: boss);

        Assert.False(session.IsGameOver);
        Assert.Contains(boss, session.Enemies);
        Assert.True(session.IsBossActive);
    }

    [Theory]
    [InlineData(39f, 0, 1, 4f)]
    [InlineData(30.1f, 0, 1, 4f)]
    [InlineData(30f, 1, 3, 8f)] // 75% of 40
    [InlineData(20f, 2, 5, 8f)] // 50%
    [InlineData(10.1f, 2, 5, 8f)]
    public void Boss_throws_more_and_faster_geminis_as_its_health_drops(float hitPointsLeft, int phases, int perThrow, float speedMultiplier)
    {
        var player = new Vector2(500, 400);
        var boss = new Enemy(EnemyRoster.GeminiBoss, new Vector2(100, 100));

        boss.TakeDamage(boss.HitPoints - hitPointsLeft);
        var minions = boss.UseAbility(player, 3f, new Random(1));

        Assert.Equal(phases, boss.PhasesReached);
        Assert.Equal(perThrow, minions.Count);
        Assert.Equal(perThrow, minions.Select(m => ((Thrown)m.Movement).LandingSpot).Distinct().Count());
        Assert.All(minions, minion =>
        {
            var start = minion.Position;
            minion.Update(player, 0.05f);
            Assert.Equal(speedMultiplier * EnemyRoster.Gemini.Speed * 0.05f, Vector2.Distance(start, minion.Position), precision: 2);
        });
        Assert.IsType<ChasePlayer>(boss.Movement);
    }

    [Fact]
    public void At_a_quarter_health_the_boss_throws_two_waves_of_five_a_quarter_second_apart()
    {
        var player = new Vector2(500, 400);
        var boss = new Enemy(EnemyRoster.GeminiBoss, new Vector2(100, 100));
        var random = new Random(1);
        boss.TakeDamage(30f);
        Assert.Equal(3, boss.PhasesReached);

        Assert.Equal(5, boss.UseAbility(player, 3f, random).Count);
        Assert.Empty(boss.UseAbility(player, 0.2f, random));
        var secondWave = boss.UseAbility(player, 0.05f, random);
        Assert.Equal(5, secondWave.Count);
        Assert.All(secondWave, minion => Assert.IsType<Thrown>(minion.Movement));
        Assert.Empty(boss.UseAbility(player, 2.7f, random));

        // The next throw, 3 seconds after the first, is again two waves.
        Assert.Equal(5, boss.UseAbility(player, 0.05f, random).Count);
    }

    [Fact]
    public void At_a_quarter_health_the_boss_moves_like_claude_at_4x_its_speed()
    {
        var boss = new Enemy(EnemyRoster.GeminiBoss, new Vector2(0, 0));
        boss.TakeDamage(29.9f);
        Assert.IsType<ChasePlayer>(boss.Movement);

        boss.TakeDamage(0.1f);
        Assert.IsType<Dash>(boss.Movement);
        Assert.Equal(EnemyRoster.Claude.CreateMovement(1f).GetType(), boss.Movement.GetType());

        // It creeps at 4x its usual speed, then lunges like Claude.
        boss.Update(new Vector2(1000, 0), 0.5f);
        Assert.Equal(4f * EnemyRoster.GeminiBoss.Speed * 0.5f, boss.Position.X, precision: 2);
    }

    [Fact]
    public void One_big_hit_applies_every_phase_it_crosses()
    {
        var boss = new Enemy(EnemyRoster.GeminiBoss, Vector2.Zero);

        boss.TakeDamage(35f);

        Assert.Equal(3, boss.PhasesReached);
        Assert.IsType<Dash>(boss.Movement);
        Assert.Equal(5, boss.UseAbility(Vector2.One * 500, 3f, new Random(1)).Count);
    }

    [Fact]
    public void Killing_blow_does_not_trigger_phases()
    {
        var boss = new Enemy(EnemyRoster.GeminiBoss, Vector2.Zero);

        boss.TakeDamage(40f);

        Assert.True(boss.IsDead);
        Assert.Equal(0, boss.PhasesReached);
    }

    [Fact]
    public void Phases_must_be_listed_from_highest_health_down()
    {
        var type = EnemyRoster.GeminiBoss with { Phases = [new BossPhase(0.25f), new BossPhase(0.75f)] };

        Assert.Throws<ArgumentException>(() => new Enemy(type, Vector2.Zero));
    }

    private static void Step(GameSession session, Enemy? toward = null)
    {
        var direction = toward is null ? Vector2.Zero : Vector2.Normalize(toward.Position - session.Ship.Position);
        session.Update(direction, null, Frame, Width, Height);
        while (session.IsChoosingUpgrade)
            session.ChooseUpgrade(0);
    }

    private static void RunUntil(GameSession session, Func<bool> done, Enemy? toward = null)
    {
        for (var i = 0; i < 60 * 300 && !done(); i++)
            Step(session, toward);
        Assert.True(done(), "Condition not reached");
    }
}
