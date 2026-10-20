using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class AutocompleteChargeTests
{
    private const float Dt = 0.1f;

    [Fact]
    public void Autocomplete_roster_entry_at_25_percent_has_null_CreateMovement_and_RetuneMovement()
    {
        var phases = EnemyRoster.Autocomplete.Phases;
        Assert.NotNull(phases);

        var phase25 = Assert.Single(phases, p => MathF.Abs(p.AtHealthFraction - 0.25f) < 0.001f);
        Assert.Null(phase25.CreateMovement);
        Assert.NotNull(phase25.RetuneMovement);

        var phase50 = Assert.Single(phases, p => MathF.Abs(p.AtHealthFraction - 0.50f) < 0.001f);
        Assert.Null(phase50.CreateMovement);
        Assert.Null(phase50.RetuneMovement);

        var phase75 = Assert.Single(phases, p => MathF.Abs(p.AtHealthFraction - 0.75f) < 0.001f);
        Assert.NotNull(phase75.RetuneMovement);

        // Retuning a sample ChargeAlongLine with phase25's action
        var sampleCharge = new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: 550f, chargeDistance: 900f);
        phase25.RetuneMovement(sampleCharge);
        // Next lock will apply speed 700, distance 900, telegraph 1.0
        sampleCharge.Move(Vector2.Zero, new Vector2(1000f, 0f), 0.1f);
        Assert.Equal(700f, sampleCharge.ChargeSpeed);
        Assert.Equal(900f, sampleCharge.ChargeDistance);
        Assert.Equal(1.0f, sampleCharge.TelegraphSeconds);
    }

    [Fact]
    public void Autocomplete_crossing_25_percent_mid_charge_preserves_movement_instance_and_finishes_at_locked_speed()
    {
        const float width = 3000f;
        const float height = 1000f;

        var shipPos = new Vector2(600f, 400f);
        var session = TestEnemies.QuietSession(shipPos);
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 400f));
        session.AddEnemy(boss);
        var initialMovement = boss.Movement;
        var charge = Assert.IsType<ChargeAlongLine>(boss.Movement);

        // Pre-damage to 35% health (21 HP, which is between 50% = 30 HP and 25% = 15 HP)
        // Crossing 75% and 50%
        boss.TakeDamage(39f);
        Assert.Equal(21f, boss.HitPoints);
        Assert.Equal(2, boss.PhasesReached);
        Assert.Same(initialMovement, boss.Movement);

        // Telegraph 1.2s to lock segment from (100, 400) to Ship (600, 400) = 500 px.
        // Between 50% and 25%, locked segment speed was set by 75% retune: 550 px/s.
        session.Update(Vector2.Zero, null, 1.2f, width, height);
        Assert.True(charge.IsCharging);
        Assert.False(charge.IsTelegraphing);
        Assert.Equal(550f, charge.ChargeSpeed);
        Assert.Equal(900f, charge.ChargeDistance);
        var lockedAimStart = charge.AimStart;
        var lockedAimEnd = charge.AimEnd;
        Assert.Equal(new Vector2(100f, 400f), lockedAimStart);
        Assert.Equal(new Vector2(600f, 400f), lockedAimEnd);

        // Step 0.2s into the charge at 550 px/s (moves 110 px -> position X = 210)
        session.Update(Vector2.Zero, null, 0.2f, width, height);
        Assert.True(charge.IsCharging);
        Assert.Equal(new Vector2(210f, 400f), boss.Position);

        // Damage below 25% threshold (25% of 60 = 15 HP; damage 7f -> 14 HP)
        boss.TakeDamage(7f);
        Assert.Equal(14f, boss.HitPoints);
        Assert.Equal(3, boss.PhasesReached);
        Assert.Same(initialMovement, boss.Movement);

        // Move ship far away to (2000, 400) to test next lock distance
        session.Ship.Move(Vector2.UnitX, 7f);
        Assert.Equal(new Vector2(2000f, 400f), session.Ship.Position);

        // Mid-charge: next step continues along old segment at old locked speed 550 px/s, NOT 700 px/s
        var posBefore = boss.Position;
        session.Update(Vector2.Zero, null, Dt, width, height);
        var stepDisplacement = boss.Position - posBefore;
        Assert.Equal(550f * Dt, stepDisplacement.X, 3);
        Assert.Equal(0f, stepDisplacement.Y, 3);
        Assert.True(charge.IsCharging);
        Assert.False(charge.IsTelegraphing);

        // Complete the in-flight charge until lockedAimEnd
        while (boss.Position.X < lockedAimEnd.X)
        {
            Assert.True(charge.IsCharging);
            Assert.False(charge.IsTelegraphing);

            var pre = boss.Position;
            session.Update(Vector2.Zero, null, Dt, width, height);

            if (boss.Position.X < lockedAimEnd.X)
            {
                Assert.Equal(550f * Dt, boss.Position.X - pre.X, 3);
                Assert.True(charge.IsCharging);
                Assert.False(charge.IsTelegraphing);
            }
            else
            {
                // Reached AimEnd
                Assert.Equal(lockedAimEnd.X, boss.Position.X, 3);
                Assert.Equal(lockedAimEnd.Y, boss.Position.Y, 3);
                Assert.False(charge.IsCharging);
                Assert.True(charge.IsTelegraphing);
            }
        }

        // Segment finished, now in telegraph.
        // The newly locked segment should have ChargeSpeed 700, ChargeDistance 900, TelegraphSeconds 1.0.
        Assert.Equal(700f, charge.ChargeSpeed);
        Assert.Equal(900f, charge.ChargeDistance);
        Assert.Equal(1.0f, charge.TelegraphSeconds);
        Assert.Equal(lockedAimEnd, charge.AimStart);
        // Ship is at 2000, distance is 1400 px, so clamped to ChargeDistance 900 px
        Assert.Equal(new Vector2(lockedAimEnd.X + 900f, lockedAimEnd.Y), charge.AimEnd);

        // Verify telegraph duration is 1.0 s (+/- 1 frame)
        // Advance 0.9s -> still telegraphing
        session.Update(Vector2.Zero, null, 0.9f, width, height);
        Assert.True(charge.IsTelegraphing);
        Assert.False(charge.IsCharging);
        Assert.Equal(lockedAimEnd, boss.Position);

        // Advance remaining 0.1s (total 1.0s) -> transitions to charge
        session.Update(Vector2.Zero, null, 0.1f, width, height);
        Assert.True(charge.IsCharging);
        Assert.False(charge.IsTelegraphing);

        // Next charge step moves at 700 px/s * Dt
        var chargeStartPos = boss.Position;
        session.Update(Vector2.Zero, null, Dt, width, height);
        var chargeStepDisplacement = boss.Position - chargeStartPos;
        Assert.Equal(700f * Dt, chargeStepDisplacement.X, 3);
        Assert.Equal(0f, chargeStepDisplacement.Y, 3);
    }

    [Fact]
    public void Autocomplete_single_hit_full_health_to_10_percent_applies_all_retunes_in_order()
    {
        const float width = 3000f;
        const float height = 1000f;

        var shipPos = new Vector2(1600f, 400f);
        var session = TestEnemies.QuietSession(shipPos);
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 400f));
        session.AddEnemy(boss);
        var initialMovement = boss.Movement;
        var charge = Assert.IsType<ChargeAlongLine>(boss.Movement);

        Assert.Equal(60f, boss.HitPoints);
        Assert.Equal(0, boss.PhasesReached);

        // One-hit full-health to 10% (6 HP): damage 54f
        boss.TakeDamage(54f);
        Assert.Equal(6f, boss.HitPoints);
        Assert.False(boss.IsDead);
        Assert.Equal(3, boss.PhasesReached);
        Assert.Same(initialMovement, boss.Movement);

        // Lock first segment during telegraph
        session.Update(Vector2.Zero, null, 0.1f, width, height);

        // Retunes applied in order: 0.75 retunes to 550/900; 0.25 retunes to 700/900/1.0s.
        // Once locked: ChargeSpeed = 700, ChargeDistance = 900, TelegraphSeconds = 1.0
        Assert.Equal(700f, charge.ChargeSpeed);
        Assert.Equal(900f, charge.ChargeDistance);
        Assert.Equal(1.0f, charge.TelegraphSeconds);
    }

    [Fact]
    public void Autocomplete_between_50_and_25_percent_charge_moves_at_550()
    {
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 400f));
        var charge = Assert.IsType<ChargeAlongLine>(boss.Movement);

        // Cross 75% and 50% (damage 35f -> 25 HP, which is between 30 HP and 15 HP)
        boss.TakeDamage(35f);
        Assert.Equal(2, boss.PhasesReached);

        // Lock segment
        boss.Update(new Vector2(800f, 400f), 1.2f);
        Assert.True(charge.IsCharging);
        Assert.Equal(550f, charge.ChargeSpeed);
        Assert.Equal(900f, charge.ChargeDistance);
        Assert.Equal(1.2f, charge.TelegraphSeconds);

        var prePos = boss.Position;
        boss.Update(new Vector2(800f, 400f), 0.1f);
        Assert.Equal(550f * 0.1f, boss.Position.X - prePos.X, 3);
    }
}
