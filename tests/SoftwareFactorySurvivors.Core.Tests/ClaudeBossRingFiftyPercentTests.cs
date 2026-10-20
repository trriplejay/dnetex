using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ClaudeBossRingFiftyPercentTests
{
    private const float Step = 1f / 64f;
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500f, 400f);

    private static ClosingRing RingOf(Enemy boss) =>
        ((CompositeAbility)boss.Ability!).Abilities.OfType<ClosingRing>().Single();

    /// <summary>How fast a ring contracts: its radius drop over one second, from a fresh hazard.</summary>
    private static float ContractionOf(Hazard ring, Vector2 center)
    {
        var before = Vector2.Distance(center, ring.Shape.Lines[0].A);
        ring.Update(1f);
        var after = Vector2.Distance(center, ring.Shape.Lines[0].A);
        return before - after;
    }

    private static void AssertRing(Hazard ring, float lifetime, float contraction)
    {
        Assert.Equal(HazardKind.Ring, ring.Kind);
        Assert.Equal(lifetime, ring.LifetimeSeconds);
        Assert.Equal(1.2f, ring.ArmSeconds);
        Assert.Equal(300f, Vector2.Distance(Center, ring.Shape.Lines[0].A), precision: 2);
        Assert.Equal(contraction, ContractionOf(ring, Center), precision: 2);
    }

    // Drives the boss's ability one step; returns the rings it spawned.
    private static List<Hazard> StepRings(Enemy boss, Random rng)
    {
        boss.UseAbility(Center, Step, rng);
        return ((IHazardProducer)boss.Ability!).TakeHazards().Where(h => h.Kind == HazardKind.Ring).ToList();
    }

    [Theory]
    [InlineData(false)] // staged: 120 -> 90 -> 60
    [InlineData(true)]  // one hit: 120 -> 30, crossing 75%, 50% and 25%
    public void Rings_after_the_50_percent_crossing_use_6s_cooldown_60px_contraction_and_6s_lifetime(bool oneHit)
    {
        var phase50 = Assert.Single(EnemyRoster.ClaudeBoss.Phases!, p => MathF.Abs(p.AtHealthFraction - 0.50f) < 0.001f);
        Assert.Null(phase50.CreateAbility);
        Assert.Null(phase50.CreateMovement);

        var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
        var rng = new Random(7);
        var ringBefore = RingOf(boss);

        // Before 50%: rings at the first update and 10 s later, each 8 s / 40 px/s.
        var spawnSteps = new List<int>();
        for (var i = 1; i <= (int)(12f / Step); i++)
        {
            foreach (var ring in StepRings(boss, rng))
            {
                spawnSteps.Add(i);
                AssertRing(ring, lifetime: 8f, contraction: 40f);
            }
        }
        Assert.Equal([1, (int)(10f / Step)], spawnSteps);

        if (oneHit)
        {
            boss.TakeDamage(90f);
            Assert.Equal(3, boss.PhasesReached);
        }
        else
        {
            boss.TakeDamage(30f);
            boss.TakeDamage(30f);
            Assert.Equal(2, boss.PhasesReached);
        }
        Assert.False(boss.IsDead);
        Assert.Same(ringBefore, RingOf(boss));

        // After 50%: the ring spawned at 10 s must expire (18 s) before the next; then every 6 s, 6 s / 60 px/s.
        var postSpawns = new List<int>();
        for (var i = (int)(12f / Step) + 1; i <= (int)(40f / Step); i++)
        {
            foreach (var ring in StepRings(boss, rng))
            {
                postSpawns.Add(i);
                AssertRing(ring, lifetime: 6f, contraction: 60f);
            }
        }

        Assert.True(postSpawns.Count >= 4);
        Assert.Equal((int)(10f / Step) + (int)(8f / Step), postSpawns[0]);
        for (var k = 1; k < postSpawns.Count; k++)
            Assert.Equal((int)(6f / Step), postSpawns[k] - postSpawns[k - 1]);

        // The 50% values hold at a later phase.
        if (!oneHit)
        {
            boss.TakeDamage(35f);
            Assert.Equal(3, boss.PhasesReached);
            Assert.Same(ringBefore, RingOf(boss));
            var lastSpawn = postSpawns[^1];
            var next = -1;
            for (var i = (int)(40f / Step) + 1; next < 0; i++)
            {
                foreach (var ring in StepRings(boss, rng))
                {
                    next = i;
                    AssertRing(ring, lifetime: 6f, contraction: 60f);
                }
            }
            Assert.Equal(lastSpawn + (int)(6f / Step), next);
        }
    }

    [Fact]
    public void Switch_is_keyed_to_the_050_entry_not_to_a_PhasesReached_index()
    {
        // A boss whose Phases list has no 0.50 entry never switches, however many phases it reaches.
        var noFifty = EnemyRoster.ClaudeBoss with { Phases = [new(0.75f), new(0.60f), new(0.25f)] };
        var boss = new Enemy(noFifty, Center);
        boss.TakeDamage(60f); // 120 -> 60: reaches 0.75 and 0.60
        Assert.Equal(2, boss.PhasesReached);
        var step = RingOf(boss).StepFor(boss);
        Assert.Equal(10f, step.CooldownSeconds);
        Assert.Equal(40f, step.ContractionSpeed);
        Assert.Equal(8f, step.LifetimeSeconds);

        // With 0.50 first in the list, crossing only 0.50 (one phase reached) switches.
        var fiftyFirst = EnemyRoster.ClaudeBoss with { Phases = [new(0.50f), new(0.25f)] };
        var other = new Enemy(fiftyFirst, Center);
        other.TakeDamage(60f);
        Assert.Equal(1, other.PhasesReached);
        var switched = RingOf(other).StepFor(other);
        Assert.Equal(6f, switched.CooldownSeconds);
        Assert.Equal(60f, switched.ContractionSpeed);
        Assert.Equal(6f, switched.LifetimeSeconds);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(7f)]
    [InlineData(9f)]
    public void In_a_GameSession_at_most_one_ring_is_out_and_the_pre_crossing_ring_lives_out_its_8s(float crossingAge)
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 1_000_000;
        // Held still below the ship (which fires upward), so only the ability's timing is in play.
        var boss = TestEnemies.Stationary(EnemyRoster.ClaudeBoss, new Vector2(500f, 780f));
        session.AddEnemy(boss);

        var seen = new HashSet<Hazard>(ReferenceEqualityComparer.Instance);
        var spawns = new List<(int Frame, Hazard Ring)>();
        int frame = 0;

        void Tick()
        {
            frame++;
            session.Update(Vector2.Zero, null, Step, Width, Height);
            var rings = session.Hazards.Where(h => h.Kind == HazardKind.Ring).ToList();
            Assert.True(rings.Count <= 1, $"{rings.Count} rings out at frame {frame}");
            foreach (var ring in rings.Where(seen.Add))
                spawns.Add((frame, ring));
        }

        // Second ring spawns at frame 640 (10 s); cross 50% when it is crossingAge old.
        var secondSpawnFrame = (int)(10f / Step);
        var crossingFrame = secondSpawnFrame + (int)(crossingAge / Step);
        while (frame < crossingFrame)
            Tick();
        Assert.Equal(2, spawns.Count);
        Assert.Equal(secondSpawnFrame, spawns[1].Frame);
        var preRing = spawns[1].Ring;

        boss.TakeDamage(60f);
        Assert.Equal(2, boss.PhasesReached);
        Assert.False(boss.IsDead);

        var end = crossingFrame + (int)(30f / Step);
        while (frame < end)
        {
            Tick();
            // The pre-crossing ring is neither removed early, shortened nor retuned.
            if (frame < secondSpawnFrame + (int)(8f / Step))
                Assert.Contains(preRing, session.Hazards);
        }
        Assert.Equal(8f, preRing.LifetimeSeconds);
        Assert.True(preRing.AgeSeconds >= 8f);
        Assert.DoesNotContain(preRing, session.Hazards);

        var post = spawns.Skip(2).ToList();
        var expectedFirst = crossingAge < 8f
            ? secondSpawnFrame + (int)(8f / Step)   // later of prev + 6 s and prev ring's expiry at prev + 8 s
            : crossingFrame + 1;                     // both passed: first update after the crossing
        Assert.Equal(expectedFirst, post[0].Frame);
        for (var k = 1; k < post.Count; k++)
            Assert.Equal((int)(6f / Step), post[k].Frame - post[k - 1].Frame);
        Assert.True(post.Count >= 4);
        Assert.All(post, s => Assert.Equal(6f, s.Ring.LifetimeSeconds));
    }
}
