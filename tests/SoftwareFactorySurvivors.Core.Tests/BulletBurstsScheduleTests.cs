using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class BulletBurstsTestsSchedule
{
    private static readonly Vector2 Center = new(500, 400);

    private static Enemy CreateTestBoss(Func<IEnemyAbility>? createAbility = null)
    {
        var type = new EnemyType(
            Name: "TestBoss",
            Radius: 50f,
            HitPoints: 100f,
            Speed: 50f,
            Experience: 100,
            DefaultSpawnsPerMinute: 0f,
            ArrivesAtSeconds: 0f,
            SpinSpeed: 0f,
            CreateMovement: _ => new StationaryMovement(),
            Outline: [],
            CreateAbility: createAbility,
            Phases:
            [
                new BossPhase(0.75f),
                new BossPhase(0.50f),
                new BossPhase(0.25f)
            ]);
        return new Enemy(type, Center);
    }

    private sealed class StationaryMovement : IMovementPattern
    {
        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) => position;
    }

    [Fact]
    public void BulletBursts_shortened_interval_at_1_5s_under_3s_base_fires_next_burst_at_2_0s()
    {
        // (a) at t=1.5 s under a 3 s base then shortened to 2 s, the next burst arrives at t=2.0 s, not t=3.5 s
        var schedule = new[] { new BurstStep(2.0f, 16) };
        var ability = new BulletBursts(
            secondsBetweenBursts: 3.0f,
            bulletsPerBurst: 12,
            phaseSchedule: schedule);
        var boss = CreateTestBoss();
        var rng = new Random(42);

        // Advance 1.5s under base schedule (3.0s interval)
        ability.Update(boss, Center, 1.5f, rng);
        Assert.Empty(ability.TakeHazards());

        // Hit boss down to 75% health to trigger phase 1
        boss.TakeDamage(25f);
        Assert.Equal(1, boss.PhasesReached);

        // Advance to t=1.9s (0.4s later) -> still no burst
        ability.Update(boss, Center, 0.4f, rng);
        Assert.Empty(ability.TakeHazards());

        // Advance to t=2.0s (0.1s later) -> burst arrives at 2.0s, not 3.5s
        ability.Update(boss, Center, 0.1f, rng);
        var hazards = ability.TakeHazards();
        Assert.NotEmpty(hazards);
        Assert.Equal(16, hazards.Count);
    }

    [Fact]
    public void BulletBursts_at_2_5s_with_2s_new_interval_fires_once_on_next_update_and_the_following_at_4_5s()
    {
        // (b) at t=2.5 s with a 2 s new interval, exactly one burst on the next update and the following at t=4.5 s
        var schedule = new[] { new BurstStep(2.0f, 16) };
        var ability = new BulletBursts(
            secondsBetweenBursts: 3.0f,
            bulletsPerBurst: 12,
            phaseSchedule: schedule);
        var boss = CreateTestBoss();
        var rng = new Random(42);

        // Advance 2.5s under base schedule
        ability.Update(boss, Center, 2.5f, rng);
        Assert.Empty(ability.TakeHazards());

        // Hit boss down to 75% health to trigger phase 1 (new interval = 2.0s)
        boss.TakeDamage(25f);
        Assert.Equal(1, boss.PhasesReached);

        // On next update, carried time 2.5s >= 2.0s triggers exactly one burst
        ability.Update(boss, Center, 0.0f, rng);
        var firstBurst = ability.TakeHazards();
        Assert.Equal(16, firstBurst.Count);

        // Advance by 2.0s to t=4.5s -> following burst arrives
        ability.Update(boss, Center, 2.0f, rng);
        var secondBurst = ability.TakeHazards();
        Assert.Equal(16, secondBurst.Count);
    }

    [Fact]
    public void BulletBursts_overdue_remainder_counts_toward_next_interval()
    {
        // With carried time 2.5s and 2.0s interval, 0.5s remainder counts toward the next 2.0s,
        // so the subsequent burst arrives after 1.5s (at t=4.0s).
        var schedule = new[] { new BurstStep(2.0f, 16) };
        var ability = new BulletBursts(
            secondsBetweenBursts: 3.0f,
            bulletsPerBurst: 12,
            phaseSchedule: schedule);
        var boss = CreateTestBoss();
        var rng = new Random(42);

        ability.Update(boss, Center, 2.5f, rng);
        boss.TakeDamage(25f);

        // First update fires overdue burst
        ability.Update(boss, Center, 0.0f, rng);
        Assert.Equal(16, ability.TakeHazards().Count);

        // Advance 1.4s -> 0.5 + 1.4 = 1.9s < 2.0s -> no burst
        ability.Update(boss, Center, 1.4f, rng);
        Assert.Empty(ability.TakeHazards());

        // Advance 0.1s -> 1.9 + 0.1 = 2.0s -> burst arrives at 1.5s after the overdue one
        ability.Update(boss, Center, 0.1f, rng);
        Assert.Equal(16, ability.TakeHazards().Count);
    }

    [Fact]
    public void BulletBursts_phases_past_schedule_length_uses_last_entry()
    {
        // (c) PhasesReached past the schedule length uses the last entry
        var schedule = new[]
        {
            new BurstStep(2.5f, 14),
            new BurstStep(2.0f, 18)
        };
        var ability = new BulletBursts(
            secondsBetweenBursts: 3.0f,
            bulletsPerBurst: 12,
            phaseSchedule: schedule);
        var boss = CreateTestBoss();
        var rng = new Random(42);

        Assert.Equal(new BurstStep(3.0f, 12), ability.StepFor(0));

        boss.TakeDamage(25f);
        Assert.Equal(1, boss.PhasesReached);
        Assert.Equal(new BurstStep(2.5f, 14), ability.StepFor(boss.PhasesReached));

        boss.TakeDamage(25f);
        Assert.Equal(2, boss.PhasesReached);
        Assert.Equal(new BurstStep(2.0f, 18), ability.StepFor(boss.PhasesReached));

        // Advance past schedule length (phase 3)
        boss.TakeDamage(25f);
        Assert.Equal(3, boss.PhasesReached);
        Assert.Equal(new BurstStep(2.0f, 18), ability.StepFor(boss.PhasesReached));
        Assert.Equal(new BurstStep(2.0f, 18), ability.StepFor(10));

        // Update uses the last entry's bullet count (18) and interval (2.0s)
        ability.Update(boss, Center, 2.0f, rng);
        var hazards = ability.TakeHazards();
        Assert.Equal(18, hazards.Count);
    }

    [Fact]
    public void BulletBursts_identical_schedule_to_base_emits_no_burst_on_threshold_frame()
    {
        // (d) a schedule identical to the base emits no burst on the threshold frame
        var schedule = new[] { new BurstStep(3.0f, 12) };
        var ability = new BulletBursts(
            secondsBetweenBursts: 3.0f,
            bulletsPerBurst: 12,
            phaseSchedule: schedule);
        var boss = CreateTestBoss();
        var rng = new Random(42);

        // Advance 1.5s under 3.0s base
        ability.Update(boss, Center, 1.5f, rng);
        Assert.Empty(ability.TakeHazards());

        // Hit boss down to trigger phase
        boss.TakeDamage(25f);
        Assert.Equal(1, boss.PhasesReached);

        // On threshold frame (dt = 0), no burst is emitted
        ability.Update(boss, Center, 0.0f, rng);
        Assert.Empty(ability.TakeHazards());

        // Advance to 2.9s total -> still no burst
        ability.Update(boss, Center, 1.4f, rng);
        Assert.Empty(ability.TakeHazards());

        // At 3.0s total -> burst emitted as scheduled
        ability.Update(boss, Center, 0.1f, rng);
        Assert.NotEmpty(ability.TakeHazards());
    }

    [Fact]
    public void BulletBursts_without_schedule_behaves_identically_to_base()
    {
        var ability = new BulletBursts(secondsBetweenBursts: 3.0f, bulletsPerBurst: 12);
        var boss = CreateTestBoss();

        Assert.Equal(new BurstStep(3.0f, 12), ability.StepFor(0));
        Assert.Equal(new BurstStep(3.0f, 12), ability.StepFor(1));
        Assert.Equal(new BurstStep(3.0f, 12), ability.StepFor(5));
        Assert.Empty(ability.PhaseSchedule);
    }
}
