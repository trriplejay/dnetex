using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class SpawnScheduleTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);
    private const float Frame = 1f / 60f;

    [Fact]
    public void Spawning_stops_from_1_20_to_1_30_and_a_ring_of_16_geminis_500_px_out_arrives_at_1_30()
    {
        Assert.Equal([new SpawnPause(80f, 90f)], EnemyRoster.SpawnPauses);
        Assert.Equal([new RingSpawn(90f, EnemyRoster.Gemini, 16, 500f)], EnemyRoster.Rings);
    }

    [Fact]
    public void Regular_spawning_halts_during_the_lull_then_resumes_at_its_normal_rate()
    {
        var session = new GameSession(Center, new Random(1), roster: [EnemyRoster.Gemini], bosses: [], rings: []);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 1_000_000;
        var seen = new HashSet<Enemy>();

        RunUntil(session, seen, 79.9f);
        Assert.NotEmpty(seen);
        Assert.False(session.IsSpawningPaused);

        // A Gemini spawns every 2 seconds normally (the one due at 1:20 may land on the boundary frame);
        // after that nothing new appears during the lull.
        RunUntil(session, seen, 80.1f);
        Assert.Empty(RunUntil(session, seen, 89.9f));
        Assert.True(session.IsSpawningPaused);

        RunUntil(session, seen, 90f);
        Assert.False(session.IsSpawningPaused);
        Assert.Equal(2, RunUntil(session, seen, 94f).Count);
    }

    [Fact]
    public void Ring_surrounds_the_ship_evenly_when_its_time_comes()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], spawnPauses: [],
            rings: [new RingSpawn(60f, EnemyRoster.Gemini, 16, 400f)], start: new StartOptions(StartSeconds: 59f));

        var seen = new HashSet<Enemy>();
        Assert.Empty(RunUntil(session, seen, 59.9f));

        var ring = RunUntil(session, seen, 60f);

        Assert.Equal(16, ring.Count);
        Assert.All(ring, enemy => Assert.Same(EnemyRoster.Gemini, enemy.Type));
        var shipAtSpawn = session.Ship.Position;
        var angles = ring
            .Select(enemy => enemy.Position - shipAtSpawn)
            .Select(offset => MathF.Atan2(offset.Y, offset.X))
            .Order()
            .ToArray();
        // They've had one frame to move toward the ship since appearing 400 px out.
        Assert.All(ring, enemy => Assert.InRange(Vector2.Distance(enemy.Position, shipAtSpawn), 398f, 400f));
        for (var i = 1; i < angles.Length; i++)
            Assert.Equal(MathF.Tau / 16f, angles[i] - angles[i - 1], precision: 3);

        // It only happens once.
        Assert.Empty(RunUntil(session, seen, 62f));
    }

    [Fact]
    public void Starting_after_the_ring_skips_it()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], spawnPauses: [],
            rings: [new RingSpawn(60f, EnemyRoster.Gemini, 16, 400f)], start: new StartOptions(StartSeconds: 61f));

        Assert.Empty(RunUntil(session, [], 62f));
    }

    /// <summary>Plays until the clock reaches the given time and returns the enemies that appeared on the way.</summary>
    private static List<Enemy> RunUntil(GameSession session, HashSet<Enemy> seen, float seconds)
    {
        var appeared = new List<Enemy>();
        while (session.ElapsedSeconds < seconds)
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            while (session.IsChoosingUpgrade)
                session.ChooseUpgrade(0);
            appeared.AddRange(session.Enemies.Where(seen.Add));
        }
        return appeared;
    }
}
