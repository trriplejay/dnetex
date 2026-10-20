using System.Numerics;
using SoftwareFactorySurvivors.Core;
using Xunit;

namespace SoftwareFactorySurvivors.Core.Tests;

public class BossPhaseRetuneTests
{
    private sealed class TestMovement(float speed) : IMovementPattern
    {
        public float Speed { get; set; } = speed;
        public List<string> History { get; } = [];

        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) => position;
    }

    private static EnemyType CreateTestType(IReadOnlyList<BossPhase> phases) =>
        new(
            "TestRetuneBoss",
            Radius: 50f,
            HitPoints: 100f,
            Speed: 100f,
            Experience: 100,
            DefaultSpawnsPerMinute: 0f,
            ArrivesAtSeconds: float.PositiveInfinity,
            SpinSpeed: 0f,
            speed => new TestMovement(speed),
            LogoShapes.Cursor(50f),
            Phases: phases);

    [Fact]
    public void Staged_crossing_keeps_same_movement_instance_and_retune_is_observed()
    {
        var phase75 = new BossPhase(0.75f, RetuneMovement: m =>
        {
            var tm = (TestMovement)m;
            tm.Speed = 500f;
            tm.History.Add("retune-75");
        });

        var type = CreateTestType([phase75]);
        var boss = new Enemy(type, Vector2.Zero);
        var initialMovement = boss.Movement;

        Assert.Equal(100f, ((TestMovement)boss.Movement).Speed);
        Assert.Equal(0, boss.PhasesReached);

        boss.TakeDamage(25f);

        Assert.Equal(75f, boss.HitPoints);
        Assert.Equal(1, boss.PhasesReached);
        Assert.Same(initialMovement, boss.Movement);
        Assert.True(ReferenceEquals(initialMovement, boss.Movement));
        Assert.Equal(500f, ((TestMovement)boss.Movement).Speed);
        Assert.Equal(["retune-75"], ((TestMovement)boss.Movement).History);
    }

    [Fact]
    public void One_hit_crossing_two_retune_phases_applies_both_in_order()
    {
        var phase75 = new BossPhase(0.75f, RetuneMovement: m =>
        {
            var tm = (TestMovement)m;
            tm.Speed = 200f;
            tm.History.Add("phase-75");
        });
        var phase50 = new BossPhase(0.50f, RetuneMovement: m =>
        {
            var tm = (TestMovement)m;
            tm.Speed = 300f;
            tm.History.Add("phase-50");
        });

        var type = CreateTestType([phase75, phase50]);
        var boss = new Enemy(type, Vector2.Zero);
        var initialMovement = boss.Movement;

        // Single hit from 100 HP to 40 HP crosses both 0.75 and 0.50
        boss.TakeDamage(60f);

        Assert.Equal(40f, boss.HitPoints);
        Assert.Equal(2, boss.PhasesReached);
        Assert.Same(initialMovement, boss.Movement);
        // Both retunes applied in order, last value wins for Speed
        Assert.Equal(300f, ((TestMovement)boss.Movement).Speed);
        Assert.Equal(["phase-75", "phase-50"], ((TestMovement)boss.Movement).History);
    }

    [Fact]
    public void Killing_blow_enters_no_phase_and_applies_no_retune()
    {
        var retuneCalled = false;
        var phase75 = new BossPhase(0.75f, RetuneMovement: m =>
        {
            retuneCalled = true;
        });

        var type = CreateTestType([phase75]);
        var boss = new Enemy(type, Vector2.Zero);

        boss.TakeDamage(100f);

        Assert.True(boss.IsDead);
        Assert.Equal(0f, boss.HitPoints);
        Assert.Equal(0, boss.PhasesReached);
        Assert.False(retuneCalled);
    }

    [Fact]
    public void CreateCurrentMovement_is_same_delegate_before_and_after_retune_only_phase()
    {
        var phase75 = new BossPhase(0.75f, RetuneMovement: m =>
        {
            ((TestMovement)m).Speed = 250f;
        });

        var type = CreateTestType([phase75]);
        var boss = new Enemy(type, Vector2.Zero);

        var delegateBefore = boss.CreateCurrentMovement;
        Assert.NotNull(delegateBefore);

        boss.TakeDamage(25f);

        var delegateAfter = boss.CreateCurrentMovement;
        Assert.Same(delegateBefore, delegateAfter);

        // Invoking CreateCurrentMovement creates a fresh instance constructed by the original factory
        var freshInstance = boss.CreateCurrentMovement();
        Assert.IsType<TestMovement>(freshInstance);
        Assert.NotSame(boss.Movement, freshInstance);
        Assert.Equal(100f, ((TestMovement)freshInstance).Speed);
    }

    [Fact]
    public void Copy_dies_in_one_hit_and_never_enters_phase_or_applies_retune()
    {
        var retuneCalled = false;
        var phase75 = new BossPhase(0.75f, RetuneMovement: _ => retuneCalled = true);
        var type = CreateTestType([phase75]);
        var copy = new Enemy(type, Vector2.Zero, isCopy: true);

        copy.TakeDamage(10f);

        Assert.True(copy.IsDead);
        Assert.Equal(0, copy.PhasesReached);
        Assert.False(retuneCalled);
    }
}
