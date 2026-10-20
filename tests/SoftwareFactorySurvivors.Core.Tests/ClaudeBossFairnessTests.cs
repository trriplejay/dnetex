using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ClaudeBossFairnessTests
{
    private static readonly Vector2 Center = new(500f, 400f);
    private const float Dt = 1f / 64f;

    [Fact]
    public void ClaudeBoss_ring_hazards_have_arm_seconds_at_least_one_and_unarmed_for_first_second_across_all_phases()
    {
        var phases = EnemyRoster.ClaudeBoss.Phases!;
        Assert.NotNull(phases);

        // Run through before any phase (phase index -1 / 0 phases reached) and after reaching each phase
        for (var phaseIndex = -1; phaseIndex < phases.Count; phaseIndex++)
        {
            var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
            var rng = new Random(100 + phaseIndex);

            if (phaseIndex >= 0)
            {
                // Stage damage to reach this phase
                var targetHp = EnemyRoster.ClaudeBoss.HitPoints * phases[phaseIndex].AtHealthFraction;
                boss.TakeDamage(boss.HitPoints - targetHp);
                Assert.Equal(phaseIndex + 1, boss.PhasesReached);
            }
            else
            {
                Assert.Equal(0, boss.PhasesReached);
            }

            var ability = (CompositeAbility)boss.Ability!;
            var producer = (IHazardProducer)ability;

            // Run for at least 30 s of UseAbility
            var ringsCollected = new List<Hazard>();
            var totalSteps = (int)MathF.Ceiling(30f / Dt);

            for (var step = 0; step < totalSteps; step++)
            {
                boss.UseAbility(Center, Dt, rng);
                var hazards = producer.TakeHazards();
                foreach (var hazard in hazards.Where(h => h.Kind == HazardKind.Ring))
                {
                    ringsCollected.Add(hazard);
                }
            }

            Assert.NotEmpty(ringsCollected);
            foreach (var ring in ringsCollected)
            {
                Assert.True(ring.ArmSeconds >= 1.0f, $"Ring hazard ArmSeconds {ring.ArmSeconds} is less than 1.0s in phase {phaseIndex}");
                Assert.False(ring.IsArmed, "Fresh ring hazard should not be armed");

                // Update to 0.99 s age
                ring.Update(0.99f);
                Assert.Equal(0.99f, ring.AgeSeconds, precision: 3);
                Assert.False(ring.IsArmed, $"Ring hazard must not be armed after aging 0.99 s (ArmSeconds: {ring.ArmSeconds})");
            }
        }
    }

    [Fact]
    public void ClosingRing_spawns_ring_with_arm_seconds_at_least_one_even_when_warmup_seconds_is_zero()
    {
        var ringAbility = new ClosingRing(warmupSeconds: 0f);
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
        var rng = new Random(42);

        ringAbility.Update(boss, Center, Dt, rng);
        var hazards = ringAbility.TakeHazards();
        var ring = Assert.Single(hazards);

        Assert.Equal(HazardKind.Ring, ring.Kind);
        Assert.True(ring.ArmSeconds >= 1.0f, $"Ring ArmSeconds {ring.ArmSeconds} must be >= 1.0s even when warmupSeconds is 0f");

        ring.Update(0.99f);
        Assert.False(ring.IsArmed, "Ring must remain unarmed at 0.99s");
    }

    [Fact]
    public void ClaudeBoss_bullet_bursts_move_slower_than_ship_and_leave_required_gap_for_all_phases()
    {
        var phases = EnemyRoster.ClaudeBoss.Phases!;
        Assert.NotNull(phases);

        // CLM-3 requires: For each phasesReached from 0 to Phases.Count + 1, drive the real ClaudeBoss ability through staged TakeDamage until a burst is emitted.
        for (var targetPhases = 0; targetPhases <= phases.Count + 1; targetPhases++)
        {
            var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
            var rng = new Random(1000 + targetPhases);

            if (targetPhases > 0)
            {
                var phaseIdx = Math.Min(targetPhases - 1, phases.Count - 1);
                var targetHp = EnemyRoster.ClaudeBoss.HitPoints * phases[phaseIdx].AtHealthFraction;
                boss.TakeDamage(boss.HitPoints - targetHp);
            }

            var ability = (CompositeAbility)boss.Ability!;
            var producer = (IHazardProducer)ability;

            // Step until a bullet burst is emitted
            List<Hazard> bullets = [];
            for (var step = 0; step < 1000 && bullets.Count == 0; step++)
            {
                boss.UseAbility(Center, Dt, rng);
                var hazards = producer.TakeHazards();
                bullets = hazards.Where(h => h.Kind == HazardKind.Bullet).ToList();
            }

            Assert.NotEmpty(bullets);

            // Assert each bullet's per-step displacement is below Ship.Speed * dt
            var angles = new List<float>();
            foreach (var bullet in bullets)
            {
                var circleInitial = Assert.Single(bullet.Shape.Circles);
                var posInitial = circleInitial.Center;

                bullet.Update(Dt);

                var circleAfter = Assert.Single(bullet.Shape.Circles);
                var posAfter = circleAfter.Center;
                var displacement = Vector2.Distance(posInitial, posAfter);

                Assert.True(displacement < Ship.Speed * Dt,
                    $"Bullet displacement {displacement} exceeds max allowed {Ship.Speed * Dt}");

                var velocity = (posAfter - posInitial) / Dt;
                var angle = MathF.Atan2(velocity.Y, velocity.X);
                while (angle < 0f) angle += MathF.Tau;
                while (angle >= MathF.Tau) angle -= MathF.Tau;
                angles.Add(angle);
            }

            // Assert wrapping angular gap exceeds MinRequiredGapRadians
            angles.Sort();
            var maxGap = 0f;
            for (var i = 0; i < angles.Count; i++)
            {
                var nextIdx = (i + 1) % angles.Count;
                var diff = (nextIdx == 0)
                    ? (MathF.Tau - angles[i] + angles[0])
                    : (angles[nextIdx] - angles[i]);
                if (diff > maxGap)
                    maxGap = diff;
            }

            Assert.True(maxGap > BulletBursts.MinRequiredGapRadians,
                $"Max gap {maxGap} rad was not greater than MinRequiredGapRadians {BulletBursts.MinRequiredGapRadians}");
        }
    }

    [Fact]
    public void BulletBursts_constructor_refuses_speed_at_or_above_ship_speed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BulletBursts(bulletSpeed: Ship.Speed));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BulletBursts(bulletSpeed: Ship.Speed + 10f));
    }

    private static readonly Vector2 FarShip = new(1_000_000f, 400f);

    private sealed class FairnessLungeDetector(Enemy boss, Vector2 shipPosition)
    {
        private int _stepIndex;
        private int _lastCreepStartStep;
        private bool _inLunge;

        public int LungesObserved { get; private set; }

        public void StepOnce()
        {
            _stepIndex++;
            var before = boss.Position;
            boss.Update(shipPosition, Dt);
            var moved = Vector2.Distance(before, boss.Position);

            // Creep is 80 px/s (1.25 px/step); lunges are 420+ px/s (6.5+ px/step).
            // A displacement above creep speed indicates the start or continuation of a lunge.
            var isLungeDisplacement = moved > 100f * Dt;

            if (isLungeDisplacement)
            {
                if (!_inLunge)
                {
                    _inLunge = true;
                    LungesObserved++;

                    var creepDuration = (_stepIndex - 1 - _lastCreepStartStep) * Dt;
                    Assert.True(creepDuration >= 1.0f - 1e-4f,
                        $"Lunge #{LungesObserved} at step {_stepIndex} started {creepDuration:F3}s after creep started at step {_lastCreepStartStep}, which is less than 1.0s");
                    Assert.InRange(creepDuration, 1.6f - 2f * Dt, 1.6f + 2f * Dt);
                }
            }
            else
            {
                if (_inLunge)
                {
                    _inLunge = false;
                    _lastCreepStartStep = _stepIndex - 1;
                }
            }
        }

        public void RunSteps(int steps)
        {
            for (var i = 0; i < steps; i++)
                StepOnce();
        }

        public void RunLunges(int count)
        {
            var target = LungesObserved + count;
            var guard = 0;
            while (LungesObserved < target)
            {
                StepOnce();
                Assert.True(++guard < 100_000, "Boss stopped lunging.");
            }
            // If in a lunge, complete it so subsequent phase begins from a known state
            while (_inLunge)
            {
                StepOnce();
                Assert.True(++guard < 100_000, "Lunge did not complete.");
            }
        }
    }

    [Fact]
    public void ClaudeBoss_lunge_starts_come_at_least_one_second_after_creep_start_staged_phases()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
        var detector = new FairnessLungeDetector(boss, FarShip);

        var phases = EnemyRoster.ClaudeBoss.Phases!;
        Assert.NotNull(phases);

        // Initial phase before any crossing (phase 0): at least 3 lunges
        var initialDash = Assert.IsType<Dash>(boss.Movement);
        Assert.True(initialDash.CreepSeconds >= 1.0f);
        Assert.Equal(1.6f, initialDash.CreepSeconds);
        detector.RunLunges(3);

        for (var i = 0; i < phases.Count; i++)
        {
            // Advance partway into creep (e.g. 0.5s) to ensure mid-creep crossing preserves fairness
            detector.RunSteps((int)(0.5f / Dt));

            var targetHp = EnemyRoster.ClaudeBoss.HitPoints * phases[i].AtHealthFraction;
            boss.TakeDamage(boss.HitPoints - targetHp);
            Assert.Equal(i + 1, boss.PhasesReached);

            var dash = Assert.IsType<Dash>(boss.Movement);
            Assert.True(dash.CreepSeconds >= 1.0f, $"CreepSeconds {dash.CreepSeconds} must be >= 1.0s at phase {i + 1}");
            Assert.Equal(1.6f, dash.CreepSeconds);

            detector.RunLunges(3);
        }
    }

    [Fact]
    public void ClaudeBoss_lunge_starts_come_at_least_one_second_after_creep_start_single_hit_crosses_all_phases()
    {
        var phases = EnemyRoster.ClaudeBoss.Phases!;
        Assert.NotNull(phases);

        // Run with staged lunges before, then advance 0.5s into creep and deliver one big hit crossing all phases down to 1 HP
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
        var detector = new FairnessLungeDetector(boss, FarShip);

        // Run at full HP first for 3 lunges
        detector.RunLunges(3);

        // 0.5s into creep, hit down to just above 0 HP (1 HP remaining)
        detector.RunSteps((int)(0.5f / Dt));
        boss.TakeDamage(boss.HitPoints - 1f);
        Assert.False(boss.IsDead);
        Assert.Equal(phases.Count, boss.PhasesReached);

        var dash = Assert.IsType<Dash>(boss.Movement);
        Assert.True(dash.CreepSeconds >= 1.0f);
        Assert.Equal(1.6f, dash.CreepSeconds);

        // Run at least 3 lunges in this phase, asserting every lunge (including the first after crossing) starts >= 1.0s after creep began
        detector.RunLunges(3);
    }

    [Fact]
    public void ClaudeBoss_lunge_starts_come_at_least_one_second_after_creep_start_immediate_single_hit()
    {
        var phases = EnemyRoster.ClaudeBoss.Phases!;
        Assert.NotNull(phases);

        // Fresh boss damaged immediately down to 1 HP
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
        boss.TakeDamage(boss.HitPoints - 1f);
        Assert.False(boss.IsDead);
        Assert.Equal(phases.Count, boss.PhasesReached);

        var dash = Assert.IsType<Dash>(boss.Movement);
        Assert.True(dash.CreepSeconds >= 1.0f);
        Assert.Equal(1.6f, dash.CreepSeconds);

        var detector = new FairnessLungeDetector(boss, FarShip);
        detector.RunLunges(3);
    }
}
