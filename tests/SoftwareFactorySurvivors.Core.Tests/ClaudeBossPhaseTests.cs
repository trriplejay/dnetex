using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ClaudeBossPhaseTests
{
    private static readonly Vector2 Center = new(500f, 400f);
    private const float Step = 1f / 64f;

    [Fact]
    public void ClaudeBoss_phases_configuration_has_75_percent_phase_without_ability_or_movement_swap()
    {
        var phases = EnemyRoster.ClaudeBoss.Phases;
        Assert.NotNull(phases);
        var phase75 = Assert.Single(phases, p => MathF.Abs(p.AtHealthFraction - 0.75f) < 0.001f);

        Assert.Equal(0.75f, phase75.AtHealthFraction);
        Assert.Null(phase75.CreateAbility);
        Assert.Null(phase75.CreateMovement);
        Assert.Null(phase75.Shield);
        Assert.Equal(120f, EnemyRoster.ClaudeBoss.HitPoints);
    }

    [Fact]
    public void ClaudeBoss_pre_and_post_75_percent_phase_bullet_and_closing_ring_behavior()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
        var rng = new Random(42);

        // Pre-75%: 120 HP, 0 phases reached
        Assert.Equal(120f, boss.HitPoints);
        Assert.Equal(0, boss.PhasesReached);

        // First step (t=0) triggers initial closing ring hazard
        boss.UseAbility(Center, Step, rng);
        var initialHazards = ((IHazardProducer)boss.Ability!).TakeHazards();
        var initialRing = Assert.Single(initialHazards);
        Assert.Equal(HazardKind.Ring, initialRing.Kind);

        // Run up to t = 12.0s in steps of 1/64s
        // We expect bullet bursts at t=3.0s, t=6.0s, t=9.0s, t=12.0s (12 bullets each)
        // and a second closing ring at t=10.0s.
        var totalStepsPrePhase = (int)MathF.Round(12.0f / Step);
        var bulletBurstTimes = new List<float>();

        // step 1 was already run at t=Step
        for (var i = 2; i <= totalStepsPrePhase; i++)
        {
            var t = i * Step;
            boss.UseAbility(Center, Step, rng);
            var hazards = ((IHazardProducer)boss.Ability!).TakeHazards();

            if (hazards.Count == 0)
                continue;

            var bullets = hazards.Where(h => h.Kind == HazardKind.Bullet).ToList();
            var rings = hazards.Where(h => h.Kind == HazardKind.Ring).ToList();

            if (rings.Count > 0)
            {
                Assert.Single(rings);
                Assert.InRange(t, 10.0f - Step, 10.0f + Step);
            }

            if (bullets.Count > 0)
            {
                bulletBurstTimes.Add(t);
                VerifyBurst(bullets, expectedCount: 12);
            }
        }

        // Pre-75% saw 4 bursts at 3s, 6s, 9s, 12s
        Assert.Equal(4, bulletBurstTimes.Count);
        Assert.InRange(bulletBurstTimes[0], 2.99f, 3.01f);
        Assert.InRange(bulletBurstTimes[1], 5.99f, 6.01f);
        Assert.InRange(bulletBurstTimes[2], 8.99f, 9.01f);
        Assert.InRange(bulletBurstTimes[3], 11.99f, 12.01f);

        // Damage boss down to <= 75% HP (30 damage: 120 -> 90 HP)
        boss.TakeDamage(30f);
        Assert.Equal(90f, boss.HitPoints);
        Assert.False(boss.IsDead);
        Assert.Equal(1, boss.PhasesReached);

        // Post-75%: bullet bursts should now arrive every 2.0s with 16 bullets each.
        // Since burst was fired at 12.0s, next bursts are at t=14.0s, 16.0s, 18.0s, 20.0s.
        var totalStepsPostPhase = (int)MathF.Round(8.0f / Step);
        var postPhaseBurstTimes = new List<float>();

        for (var i = 1; i <= totalStepsPostPhase; i++)
        {
            var elapsedSincePhase = i * Step;
            boss.UseAbility(Center, Step, rng);
            var hazards = ((IHazardProducer)boss.Ability!).TakeHazards();

            if (hazards.Count == 0)
                continue;

            var bullets = hazards.Where(h => h.Kind == HazardKind.Bullet).ToList();
            if (bullets.Count > 0)
            {
                postPhaseBurstTimes.Add(elapsedSincePhase);
                VerifyBurst(bullets, expectedCount: 16);
            }
        }

        // Post-75% saw 4 bursts at 2s intervals
        Assert.Equal(4, postPhaseBurstTimes.Count);
        Assert.InRange(postPhaseBurstTimes[0], 1.99f, 2.01f);
        Assert.InRange(postPhaseBurstTimes[1], 3.99f, 4.01f);
        Assert.InRange(postPhaseBurstTimes[2], 5.99f, 6.01f);
        Assert.InRange(postPhaseBurstTimes[3], 7.99f, 8.01f);
    }

    [Fact]
    public void ClaudeBoss_75_percent_values_hold_for_higher_phases_reached()
    {
        // When later tasks append 0.50 and 0.25 phases without their own BulletBursts schedule entries,
        // StepFor returns the 75% values (2.0s, 16 bullets) for any PhasesReached >= 1.
        var ability = (CompositeAbility)EnemyRoster.ClaudeBoss.CreateAbility!();
        var bulletBursts = ability.Abilities.OfType<BulletBursts>().Single();

        Assert.Equal(new BurstStep(3.0f, 12), bulletBursts.StepFor(0));
        Assert.Equal(new BurstStep(2.0f, 16), bulletBursts.StepFor(1));
        Assert.Equal(new BurstStep(2.0f, 16), bulletBursts.StepFor(2));
        Assert.Equal(new BurstStep(2.0f, 16), bulletBursts.StepFor(3));
    }

    [Fact]
    public void ClaudeBoss_crossing_75_percent_threshold_carries_over_ability_and_timers()
    {
        AssertPhaseCarryOver(
            EnemyRoster.ClaudeBoss,
            damage: 30f,
            expectedPhasesReached: 1);
    }

    [Fact]
    public void ClaudeBoss_multi_phase_crossing_in_single_hit_carries_over_ability_and_timers()
    {
        var testBossType = EnemyRoster.ClaudeBoss with
        {
            Phases =
            [
                new(0.75f),
                new(0.50f),
                new(0.25f),
            ]
        };

        // Full health is 120 HP. One hit takes it to <= 25% (30 HP) while staying alive (e.g. 95 damage -> 25 HP).
        AssertPhaseCarryOver(
            testBossType,
            damage: 95f,
            expectedPhasesReached: 3);
    }

    private static void AssertPhaseCarryOver(EnemyType bossType, float damage, int expectedPhasesReached)
    {
        var boss = new Enemy(bossType, Center);
        var rng = new Random(42);

        var abilityBefore = boss.Ability;
        Assert.NotNull(abilityBefore);
        var compBefore = Assert.IsType<CompositeAbility>(abilityBefore);
        var subAbilitiesBefore = compBefore.Abilities.ToArray();

        // (1) Drive for 4.0s (first ring at t=0 / step 1)
        boss.UseAbility(Center, Step, rng);
        var initialHazards = ((IHazardProducer)boss.Ability!).TakeHazards();
        var initialRing = Assert.Single(initialHazards, h => h.Kind == HazardKind.Ring);
        Assert.NotNull(initialRing);

        var stepsTo4s = (int)MathF.Round(4.0f / Step);
        for (var step = 2; step <= stepsTo4s; step++)
        {
            boss.UseAbility(Center, Step, rng);
            var hazards = ((IHazardProducer)boss.Ability!).TakeHazards();
            Assert.DoesNotContain(hazards, h => h.Kind == HazardKind.Ring);
        }

        // Damage the boss across threshold(s)
        boss.TakeDamage(damage);
        Assert.False(boss.IsDead);
        Assert.Equal(expectedPhasesReached, boss.PhasesReached);

        // Assert.Same on Ability and on each CompositeAbility.Abilities entry before vs after
        Assert.Same(abilityBefore, boss.Ability);
        var compAfter = Assert.IsType<CompositeAbility>(boss.Ability);
        Assert.Equal(subAbilitiesBefore.Length, compAfter.Abilities.Count);
        for (var i = 0; i < subAbilitiesBefore.Length; i++)
        {
            Assert.Same(subAbilitiesBefore[i], compAfter.Abilities[i]);
        }

        // Assert zero Ring hazards on the threshold update
        boss.UseAbility(Center, Step, rng);
        var thresholdHazards = ((IHazardProducer)boss.Ability!).TakeHazards();
        Assert.DoesNotContain(thresholdHazards, h => h.Kind == HazardKind.Ring);

        // Next Ring at t=10 s, and Ring and Bullet hazards still produced over the following 30 s
        var ringTimes = new List<float>();
        var bulletCount = thresholdHazards.Count(h => h.Kind == HazardKind.Bullet);

        var totalSteps = (int)MathF.Round(40.0f / Step);
        for (var step = stepsTo4s + 2; step <= totalSteps; step++)
        {
            var t = step * Step;
            boss.UseAbility(Center, Step, rng);
            var hazards = ((IHazardProducer)boss.Ability!).TakeHazards();

            foreach (var h in hazards)
            {
                if (h.Kind == HazardKind.Ring)
                    ringTimes.Add(t);
                else if (h.Kind == HazardKind.Bullet)
                    bulletCount++;
            }
        }

        Assert.NotEmpty(ringTimes);
        var expectedNextRingTime = expectedPhasesReached >= 2 ? 8.0f : 10.0f;
        Assert.InRange(ringTimes[0], expectedNextRingTime - Step, expectedNextRingTime + Step);

        var ringsFollowing = ringTimes.Where(t => t > expectedNextRingTime + Step).ToList();
        Assert.NotEmpty(ringsFollowing);
        Assert.True(bulletCount > 0, "Bullet hazards should continue to be produced over the following 30 seconds.");
    }

    private static void VerifyBurst(IReadOnlyList<Hazard> bullets, int expectedCount)
    {
        Assert.Equal(expectedCount, bullets.Count);

        var minRequiredGap = BulletBursts.ShipDiameter / BulletBursts.MaxPlayFieldRadius;
        var angles = new List<float>();

        foreach (var bullet in bullets)
        {
            Assert.Equal(HazardKind.Bullet, bullet.Kind);
            Assert.True(bullet.ArmSeconds >= 1.0f);

            var initialCircle = Assert.Single(bullet.Shape.Circles);
            var initialPos = initialCircle.Center;

            bullet.Update(0.1f);
            var nextCircle = Assert.Single(bullet.Shape.Circles);
            var velocity = (nextCircle.Center - initialPos) / 0.1f;
            var speed = velocity.Length();

            Assert.True(speed < Ship.Speed, $"Bullet speed {speed} must be strictly slower than Ship.Speed {Ship.Speed}");

            var angle = MathF.Atan2(velocity.Y, velocity.X);
            while (angle < 0f) angle += MathF.Tau;
            while (angle >= MathF.Tau) angle -= MathF.Tau;
            angles.Add(angle);
        }

        angles.Sort();

        var maxGap = 0f;
        for (var i = 0; i < angles.Count; i++)
        {
            var next = (i + 1) % angles.Count;
            var diff = next == 0
                ? MathF.Tau - angles[i] + angles[0]
                : angles[next] - angles[i];

            if (diff > maxGap)
                maxGap = diff;
        }

        Assert.True(maxGap > minRequiredGap, $"Max gap {maxGap} rad was not greater than required minimum {minRequiredGap} rad");
        Assert.True(maxGap >= (MathF.PI / 3f) - 0.05f, $"Max gap {maxGap} rad must be at least 60 degrees (~{MathF.PI / 3f} rad)");
    }
}
