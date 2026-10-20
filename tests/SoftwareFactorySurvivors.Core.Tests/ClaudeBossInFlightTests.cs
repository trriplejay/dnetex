using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>
/// What a Claude Boss threshold crossing does to things already under way: a creep, a lunge and a closing ring
/// all carry on with the values they began with; only what begins afterwards uses the new ones.
/// </summary>
public class ClaudeBossInFlightTests
{
    private const float Step = 1f / 64f;
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Start = new(0f, 0f);
    // Far along +x, so every lunge before the ship moves runs along x and never reaches it.
    private static readonly Vector2 PlayerBefore = new(1_000_000f, 0f);
    // After the crossing the ship has moved sideways: far along +y.
    private static readonly Vector2 PlayerAfter = new(0f, 1_000_000f);

    // Creep moves 80 px/s (1.25 px a step); lunges move 420+ px/s (6.5+ px a step).
    private const float LungeStepThreshold = 200f * Step;
    private const float Tolerance = 1e-3f;
    // A lunge lasts 0.45 s: 29 fixed steps (the first step of a lunge already moves).
    private const int LungeSteps = 29;

    // Cumulative health: 120 -> 90 (75%) -> 60 (50%) -> 25 (25%). A hit of 0 is skipped.
    [Theory]
    [InlineData(false, 30f, 0f, 0f)]    // 75% only
    [InlineData(false, 30f, 30f, 0f)]   // 75% then 50%
    [InlineData(false, 30f, 30f, 35f)]  // 75%, 50% then 25%, staged
    [InlineData(false, 95f, 0f, 0f)]    // one hit to 25 HP, crossing all three
    [InlineData(true, 30f, 0f, 0f)]
    [InlineData(true, 30f, 30f, 0f)]
    [InlineData(true, 30f, 30f, 35f)]
    [InlineData(true, 95f, 0f, 0f)]
    public void A_creep_or_lunge_under_way_at_a_crossing_is_not_restarted_or_retuned_and_only_later_lunges_use_the_new_speed(
        bool crossDuringLunge, float hit1, float hit2, float hit3)
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Start);
        var control = new Enemy(EnemyRoster.ClaudeBoss, Start); // never damaged
        var dash = boss.Movement;
        var crossesFifty = hit1 + hit2 + hit3 >= 60f;
        var laterSpeed = crossesFifty ? 560f : 420f;

        // Lockstep to the crossing: 0.8 s into the first creep (51 steps), or 0.2 s into the first lunge (13 lunge steps).
        var lungeStepsSoFar = 0;
        if (crossDuringLunge)
        {
            while (lungeStepsSoFar < 13)
            {
                if (StepBoth(boss, control, PlayerBefore).Lunging)
                    lungeStepsSoFar++;
            }
        }
        else
        {
            for (var i = 0; i < 51; i++)
                Assert.False(StepBoth(boss, control, PlayerBefore).Lunging);
        }

        foreach (var hit in new[] { hit1, hit2, hit3 }.Where(h => h > 0f))
        {
            boss.TakeDamage(hit);
            Assert.False(boss.IsDead);
            Assert.Same(dash, boss.Movement);
        }
        Assert.Equal(crossesFifty ? 2 : 1, Math.Min(boss.PhasesReached, 2));

        // The ship has moved sideways: the boss now sees it far along +y, the control still far along +x.
        var inFlight = 0;
        var lungesAfter = new List<int>();
        var inLunge = crossDuringLunge;
        var runLength = 0;
        var lungeIndex = 0; // lunge 0 is the one under way at the crossing, if any; a creep crossing has none
        for (var i = 0; i < 6 * 64; i++)
        {
            var (lunging, bossMove, controlMove) = StepBoth(boss, control, PlayerAfter);
            Assert.Equal(lunging, bossMove.Length() > LungeStepThreshold);

            if (lunging && !inLunge)
            {
                lungeIndex++;
                runLength = 0;
            }
            if (!lunging && inLunge)
                lungesAfter.Add(runLength);
            inLunge = lunging;
            if (!lunging)
            {
                Assert.Equal(controlMove.Length(), bossMove.Length(), Tolerance);
                continue;
            }

            runLength++;
            if (lungeIndex == 0)
            {
                // Same speed and the same locked direction (+x) as the undamaged control, whatever the ship does.
                inFlight++;
                Assert.Equal(420f * Step, bossMove.Length(), Tolerance);
                Assert.Equal(controlMove.X, bossMove.X, Tolerance);
                Assert.Equal(0f, bossMove.Y, Tolerance);
            }
            else
            {
                Assert.Equal(laterSpeed * Step, bossMove.Length(), Tolerance);
            }
        }

        Assert.Same(dash, boss.Movement);
        if (crossDuringLunge)
        {
            // 13 steps before the crossing + the rest after make a full 0.45 s lunge.
            Assert.Equal(LungeSteps, 13 + inFlight);
            Assert.Equal(LungeSteps - 13, lungesAfter[0]);
        }
        Assert.True(lungesAfter.Count >= (crossDuringLunge ? 3 : 2));
        Assert.All(lungesAfter.Skip(crossDuringLunge ? 1 : 0), length => Assert.Equal(LungeSteps, length));
    }

    // Steps both bosses one frame; reports whether the control lunged and each one's displacement.
    private static (bool Lunging, Vector2 BossMove, Vector2 ControlMove) StepBoth(Enemy boss, Enemy control, Vector2 bossPlayer)
    {
        var bossBefore = boss.Position;
        var controlBefore = control.Position;
        boss.Update(bossPlayer, Step);
        control.Update(PlayerBefore, Step);
        var controlMove = control.Position - controlBefore;
        return (controlMove.Length() > LungeStepThreshold, boss.Position - bossBefore, controlMove);
    }

    private static float RingRadius(Hazard ring, Vector2 center) =>
        Vector2.Distance(center, ring.Shape.Lines[0].A);

    [Fact]
    public void A_ring_out_at_a_crossing_keeps_contracting_at_40_and_expires_8_s_after_it_spawned_while_later_rings_use_60_and_6()
    {
        var center = new Vector2(500f, 400f);
        var session = TestEnemies.QuietSession(center);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 1_000_000;
        // Held still below the ship (which fires upward), so only the ability's timing is in play.
        var boss = TestEnemies.Stationary(EnemyRoster.ClaudeBoss, new Vector2(500f, 780f));
        session.AddEnemy(boss);

        var frame = 0;
        var spawnFrames = new Dictionary<Hazard, int>(ReferenceEqualityComparer.Instance);
        var order = new List<Hazard>();
        var samples = new List<(Hazard Ring, int Frame, float Radius)>();
        var lastFramePresent = new Dictionary<Hazard, int>(ReferenceEqualityComparer.Instance);

        void Tick()
        {
            frame++;
            session.Update(Vector2.Zero, null, Step, Width, Height);
            foreach (var ring in session.Hazards.Where(h => h.Kind == HazardKind.Ring))
            {
                if (spawnFrames.TryAdd(ring, frame))
                    order.Add(ring);
                lastFramePresent[ring] = frame;
                samples.Add((ring, frame, RingRadius(ring, center)));
            }
        }

        Tick();
        var first = Assert.Single(order);
        Assert.Equal(1, spawnFrames[first]);
        Assert.Equal(300f, RingRadius(first, center), precision: 2);

        // 2 s later, a single hit takes the boss across 75%, 50% and 25% while it stays alive.
        for (var i = 0; i < (int)(2f / Step); i++)
            Tick();
        boss.TakeDamage(90f);
        Assert.Equal(3, boss.PhasesReached);
        Assert.False(boss.IsDead);

        for (var i = 0; i < (int)(20f / Step); i++)
            Tick();

        // The ring out at the crossing: 40 px/s down to the 60 px minimum, gone exactly 8 s after it spawned.
        Assert.Equal(8f, first.LifetimeSeconds);
        foreach (var (ring, sampleFrame, radius) in samples.Where(s => ReferenceEquals(s.Ring, first)))
        {
            var age = (sampleFrame - spawnFrames[first]) * Step;
            Assert.Equal(MathF.Max(60f, 300f - 40f * age), radius, precision: 1);
        }
        Assert.Equal(60f, samples.Last(s => ReferenceEquals(s.Ring, first)).Radius, precision: 1);
        Assert.Equal((int)(8f / Step) - 1, lastFramePresent[first] - spawnFrames[first]);
        Assert.True(first.AgeSeconds >= 8f);
        Assert.DoesNotContain(first, session.Hazards);

        // Only a ring spawned after the crossing uses 60 px/s and 6 s.
        var second = order[1];
        Assert.Equal(6f, second.LifetimeSeconds);
        foreach (var (ring, sampleFrame, radius) in samples.Where(s => ReferenceEquals(s.Ring, second)))
        {
            var age = (sampleFrame - spawnFrames[second]) * Step;
            Assert.Equal(MathF.Max(60f, 300f - 60f * age), radius, precision: 1);
        }
        Assert.Equal((int)(6f / Step) - 1, lastFramePresent[second] - spawnFrames[second]);
    }
}
