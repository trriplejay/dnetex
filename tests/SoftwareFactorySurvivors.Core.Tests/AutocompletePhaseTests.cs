using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class AutocompletePhaseTests
{
    [Fact]
    public void ChargeAlongLine_mid_charge_retune_finishes_on_original_segment_at_original_speed_and_ends_at_aim_end()
    {
        // (a) Retune(550, 900) mid-charge: every remaining step lies on the original locked segment,
        // the per-step displacement is 400*dt, IsCharging stays true until the original AimEnd,
        // and the charge ends there with no telegraph frame before it.
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: 400f, chargeDistance: 700f);
        var startPos = new Vector2(0f, 0f);
        var playerPos = new Vector2(600f, 0f);

        // Telegraph for 1.2 seconds to reach charge state
        var pos = pattern.Move(startPos, playerPos, 1.2f);
        Assert.True(pattern.IsCharging);
        Assert.False(pattern.IsTelegraphing);
        var originalAimStart = pattern.AimStart;
        var originalAimEnd = pattern.AimEnd;
        Assert.Equal(startPos, originalAimStart);
        Assert.Equal(playerPos, originalAimEnd);

        // Advance 0.5s into charge at 400 px/s (displacement = 200)
        const float dt = 0.5f;
        pos = pattern.Move(pos, playerPos, dt);
        Assert.Equal(new Vector2(200f, 0f), pos);
        Assert.True(pattern.IsCharging);

        // Retune mid-charge
        pattern.Retune(550f, 900f);

        // Retune never touches IsCharging, IsTelegraphing, or AimStart/AimEnd
        Assert.True(pattern.IsCharging);
        Assert.False(pattern.IsTelegraphing);
        Assert.Equal(originalAimStart, pattern.AimStart);
        Assert.Equal(originalAimEnd, pattern.AimEnd);

        // Next step of charge: player moves away, but charge must ignore player and remain on segment
        var awayPlayer = new Vector2(9999f, 9999f);
        var nextPos = pattern.Move(pos, awayPlayer, dt);

        // Per-step displacement is 400*dt (200 px), not 550*dt
        Assert.Equal(200f, nextPos.X - pos.X);
        Assert.Equal(0f, nextPos.Y);
        Assert.Equal(new Vector2(400f, 0f), nextPos);
        Assert.True(pattern.IsCharging);
        Assert.False(pattern.IsTelegraphing);
        pos = nextPos;

        // Final step reaching AimEnd (remaining 200 px)
        nextPos = pattern.Move(pos, awayPlayer, dt);
        Assert.Equal(200f, nextPos.X - pos.X);
        Assert.Equal(originalAimEnd, nextPos);
        // At AimEnd, charge completes and transitions to telegraph with no telegraph frame inserted before AimEnd
        Assert.False(pattern.IsCharging);
        Assert.True(pattern.IsTelegraphing);
    }

    [Fact]
    public void ChargeAlongLine_retune_during_telegraph_preserves_timer_and_first_charge_uses_original_segment_and_speed()
    {
        // (b) Retune during telegraph: the time to first displacement is unchanged
        // and that charge follows the already-telegraphed segment.
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: 400f, chargeDistance: 700f);
        var startPos = new Vector2(0f, 0f);
        var playerPos = new Vector2(500f, 0f);

        // Advance 0.4s into 1.2s telegraph
        var pos = pattern.Move(startPos, playerPos, 0.4f);
        Assert.Equal(startPos, pos);
        Assert.True(pattern.IsTelegraphing);
        Assert.False(pattern.IsCharging);
        var lockedAimEnd = pattern.AimEnd;
        Assert.Equal(playerPos, lockedAimEnd);

        // Retune during telegraph
        pattern.Retune(550f, 900f);

        // Telegraph state and locked segment are unchanged
        Assert.True(pattern.IsTelegraphing);
        Assert.False(pattern.IsCharging);
        Assert.Equal(lockedAimEnd, pattern.AimEnd);

        // Advance another 0.4s (total 0.8s < 1.2s) - still no displacement
        pos = pattern.Move(pos, playerPos, 0.4f);
        Assert.Equal(startPos, pos);
        Assert.True(pattern.IsTelegraphing);

        // Advance remaining 0.4s (total 1.2s) - reaches charge state on boundary, no displacement yet
        pos = pattern.Move(pos, playerPos, 0.4f);
        Assert.Equal(startPos, pos);
        Assert.True(pattern.IsCharging);
        Assert.False(pattern.IsTelegraphing);

        // First charge displacement step: moves at original speed 400 px/s along already-telegraphed segment
        const float dt = 0.1f;
        var nextPos = pattern.Move(pos, new Vector2(9999f, 9999f), dt);
        Assert.Equal(400f * dt, nextPos.X - pos.X);
        Assert.Equal(0f, nextPos.Y);
        Assert.True(pattern.IsCharging);
    }

    [Fact]
    public void ChargeAlongLine_subsequent_cycle_after_retune_locks_new_distance_and_moves_at_new_speed()
    {
        // (c) The cycle after that, with the player 850 px away, locks an 850 px segment and moves at 550*dt.
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: 400f, chargeDistance: 700f);
        var startPos = new Vector2(0f, 0f);
        var playerPos = new Vector2(500f, 0f);

        // Telegraph, start charging, then retune mid-charge
        var pos = pattern.Move(startPos, playerPos, 1.2f);
        pos = pattern.Move(pos, playerPos, 0.1f);
        Assert.True(pattern.IsCharging);
        pattern.Retune(550f, 900f);

        // The player is 850 px past where the first (500 px) segment ends when that charge completes.
        var playerAtLock = new Vector2(500f + 850f, 0f);
        while (pattern.IsCharging)
            pos = pattern.Move(pos, playerAtLock, 0.1f);

        Assert.True(pattern.IsTelegraphing);
        Assert.Equal(new Vector2(500f, 0f), pos);

        // The next segment is min(new ChargeDistance 900, 850 to the player) = 850 px long
        Assert.Equal(pos, pattern.AimStart);
        Assert.Equal(playerAtLock, pattern.AimEnd);
        Assert.Equal(850f, Vector2.Distance(pattern.AimStart, pattern.AimEnd));

        // Complete the 1.2 s telegraph, then charge at the new speed
        pos = pattern.Move(pos, playerAtLock, 1.2f);
        Assert.True(pattern.IsCharging);

        const float dt = 0.1f;
        var chargeStepPos = pattern.Move(pos, playerAtLock, dt);
        Assert.Equal(550f * dt, chargeStepPos.X - pos.X, 3);
        Assert.Equal(0f, chargeStepPos.Y);
    }

    [Fact]
    public void ChargeAlongLine_telegraph_seconds_stays_clamped_to_at_least_one_second()
    {
        var pattern = new ChargeAlongLine(telegraphSeconds: 0.5f, chargeSpeed: 400f, chargeDistance: 700f);
        Assert.Equal(1.0f, pattern.TelegraphSeconds);
    }

    [Fact]
    public void Autocomplete_phase_at_75_percent_retunes_charge_to_550_speed_and_900_distance()
    {
        // Find the phase by AtHealthFraction 0.75, not by index
        Assert.NotNull(EnemyRoster.Autocomplete.Phases);
        var phase75 = Assert.Single(EnemyRoster.Autocomplete.Phases, p => Math.Abs(p.AtHealthFraction - 0.75f) < 0.001f);
        Assert.Null(phase75.CreateMovement);
        Assert.NotNull(phase75.RetuneMovement);

        const float dt = 0.1f;

        // (1) An Enemy(EnemyRoster.Autocomplete) damaged to <=75% with the ship 800 px away:
        // its next lock is 800 px long and charges at 550*dt.
        var enemy = new Enemy(EnemyRoster.Autocomplete, new Vector2(0f, 0f));
        var charge = Assert.IsType<ChargeAlongLine>(enemy.Movement);

        // Run initial telegraph and charge cycle before threshold
        var initialPlayer = new Vector2(500f, 0f);
        enemy.Update(initialPlayer, 1.2f); // completes telegraph
        Assert.True(charge.IsCharging);

        // Damage enemy to <= 75% health (Autocomplete has 60 HP; 45 HP is 75%)
        enemy.TakeDamage(15f);
        Assert.Equal(45f, enemy.HitPoints);
        Assert.True(enemy.PhasesReached >= 1);

        // Step through remaining charge of the first cycle with ship positioned for next lock
        // Enemy will end first charge at (500, 0)
        var ship800Away = new Vector2(500f + 800f, 0f);
        while (charge.IsCharging)
            enemy.Update(ship800Away, dt);

        // After the threshold, next locked segment with ship 800 px away is min(900, 800) = 800 px long
        Assert.True(charge.IsTelegraphing);
        Assert.Equal(new Vector2(500f, 0f), enemy.Position);
        Assert.Equal(enemy.Position, charge.AimStart);
        Assert.Equal(ship800Away, charge.AimEnd);
        var lockedLength = Vector2.Distance(charge.AimStart, charge.AimEnd);
        Assert.Equal(800f, lockedLength, 3);

        // Complete telegraph for this new cycle
        enemy.Update(ship800Away, 1.2f);
        Assert.True(charge.IsCharging);

        // Charge step moves at 550 * dt
        var posBeforeStep = enemy.Position;
        enemy.Update(ship800Away, dt);
        var stepDisplacement = Vector2.Distance(enemy.Position, posBeforeStep);
        Assert.Equal(550f * dt, stepDisplacement, 3);

        // (2) A full-health control with the same geometry locks 700 and steps 400*dt.
        var control = new Enemy(EnemyRoster.Autocomplete, new Vector2(0f, 0f));
        var controlCharge = Assert.IsType<ChargeAlongLine>(control.Movement);

        // Run initial cycle for control
        var controlInitialPlayer = new Vector2(500f, 0f);
        control.Update(controlInitialPlayer, 1.2f);
        var controlShip800Away = new Vector2(500f + 800f, 0f);
        while (controlCharge.IsCharging)
            control.Update(controlShip800Away, dt);

        // Next lock at full health with ship 800 px away locks to min(700, 800) = 700 px
        Assert.True(controlCharge.IsTelegraphing);
        Assert.Equal(new Vector2(500f, 0f), control.Position);
        Assert.Equal(control.Position, controlCharge.AimStart);
        Assert.Equal(new Vector2(500f + 700f, 0f), controlCharge.AimEnd);
        var controlLockedLength = Vector2.Distance(controlCharge.AimStart, controlCharge.AimEnd);
        Assert.Equal(700f, controlLockedLength, 3);

        // Complete telegraph for control
        control.Update(controlShip800Away, 1.2f);
        Assert.True(controlCharge.IsCharging);

        // Charge step moves at 400 * dt
        var controlPosBeforeStep = control.Position;
        control.Update(controlShip800Away, dt);
        var controlStepDisplacement = Vector2.Distance(control.Position, controlPosBeforeStep);
        Assert.Equal(400f * dt, controlStepDisplacement, 3);

        // (3) After the threshold with the ship 300 px away, the charge ends at 300 px.
        // Finish enemy's 800 px charge with ship positioned 300 px past destination (500 + 800 = 1300)
        var lockStart = new Vector2(1300f, 0f);
        var ship300Away = lockStart + new Vector2(300f, 0f);
        while (charge.IsCharging)
            enemy.Update(ship300Away, dt);

        Assert.True(charge.IsTelegraphing);
        Assert.Equal(lockStart, enemy.Position);

        // Next segment locked with ship 300 px away
        Assert.Equal(lockStart, charge.AimStart);
        Assert.Equal(ship300Away, charge.AimEnd);
        Assert.Equal(300f, Vector2.Distance(charge.AimStart, charge.AimEnd), 3);

        // Complete telegraph
        enemy.Update(ship300Away, 1.2f);
        Assert.True(charge.IsCharging);

        // Step until charge ends
        while (charge.IsCharging)
            enemy.Update(ship300Away, dt);

        Assert.True(charge.IsTelegraphing);
        Assert.Equal(ship300Away, enemy.Position);
        Assert.Equal(300f, Vector2.Distance(enemy.Position, lockStart), 3);
    }

    [Fact]
    public void GameSession_mid_charge_damage_below_75_percent_preserves_movement_finishes_old_segment_and_retunes_next_charge()
    {
        const float width = 3000f;
        const float height = 1000f;
        const float dt = 0.1f;

        var initialShipPos = new Vector2(600f, 400f);
        var session = TestEnemies.QuietSession(initialShipPos);
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 400f));
        session.AddEnemy(boss);
        var charge = Assert.IsType<ChargeAlongLine>(boss.Movement);

        // 1. Telegraph for 1.2s to lock segment (100, 400) -> (600, 400) and enter charge state
        session.Update(Vector2.Zero, null, 1.2f, width, height);
        Assert.True(charge.IsCharging);
        Assert.False(charge.IsTelegraphing);
        var oldAimStart = charge.AimStart;
        var oldAimEnd = charge.AimEnd;
        Assert.Equal(new Vector2(100f, 400f), oldAimStart);
        Assert.Equal(new Vector2(600f, 400f), oldAimEnd);
        Assert.Equal(new Vector2(100f, 400f), boss.Position);

        // 2. Advance mid-charge: moves at 400 px/s along locked segment
        session.Update(Vector2.Zero, null, dt, width, height);
        Assert.True(charge.IsCharging);
        Assert.False(charge.IsTelegraphing);
        Assert.Equal(new Vector2(140f, 400f), boss.Position);
        Assert.NotEmpty(session.Hazards);
        Assert.All(session.Hazards, h => Assert.Equal(HazardKind.Trail, h.Kind));

        // 3. TakeDamage takes Autocomplete below 75% health (60 HP * 0.75 = 45 HP)
        boss.TakeDamage(20f);
        Assert.Equal(40f, boss.HitPoints);
        Assert.True(boss.PhasesReached >= 1);

        // Assert: boss.Movement is the same ChargeAlongLine reference
        Assert.Same(charge, boss.Movement);

        // Move ship away to (1500, 400) so the next segment has 900 px distance available
        session.Ship.Move(Vector2.UnitX, 4.5f);
        Assert.Equal(new Vector2(1500f, 400f), session.Ship.Position);

        // 4. Assert: displacement continues along the old segment at 400*dt
        var posBeforeMidStep = boss.Position;
        session.Update(Vector2.Zero, null, dt, width, height);
        var midStepDisplacement = boss.Position - posBeforeMidStep;
        Assert.Equal(400f * dt, midStepDisplacement.X, 3);
        Assert.Equal(0f, midStepDisplacement.Y, 3);
        Assert.True(charge.IsCharging);
        Assert.False(charge.IsTelegraphing);

        // 5. Assert: IsCharging stays true continuously until the old AimEnd, with no telegraph frame inserted
        while (boss.Position.X < oldAimEnd.X)
        {
            Assert.True(charge.IsCharging);
            Assert.False(charge.IsTelegraphing);

            var preStep = boss.Position;
            session.Update(Vector2.Zero, null, dt, width, height);

            if (boss.Position.X < oldAimEnd.X)
            {
                Assert.Equal(400f * dt, boss.Position.X - preStep.X, 3);
                Assert.Equal(0f, boss.Position.Y - preStep.Y, 3);
                Assert.True(charge.IsCharging);
                Assert.False(charge.IsTelegraphing);
            }
            else
            {
                // Reached old AimEnd
                Assert.Equal(oldAimEnd.X, boss.Position.X, 3);
                Assert.Equal(oldAimEnd.Y, boss.Position.Y, 3);
                // Transition to telegraph happens at old AimEnd, not before
                Assert.False(charge.IsCharging);
                Assert.True(charge.IsTelegraphing);
            }

            // Assert: trail hazards keep covering the path
            Assert.NotEmpty(session.Hazards);
            Assert.All(session.Hazards, h => Assert.Equal(HazardKind.Trail, h.Kind));
        }

        // The next segment is locked from old AimEnd (600, 400) to Ship (1500, 400) = 900 px
        Assert.Equal(oldAimEnd, charge.AimStart);
        Assert.Equal(session.Ship.Position, charge.AimEnd);
        Assert.Equal(900f, Vector2.Distance(charge.AimStart, charge.AimEnd), 3);
        Assert.Equal(550f, charge.ChargeSpeed);
        Assert.Equal(900f, charge.ChargeDistance);

        // 6. Complete telegraph for the next cycle
        session.Update(Vector2.Zero, null, 1.2f, width, height);
        Assert.True(charge.IsCharging);

        // 7. Assert: the next charge steps 550*dt
        var posBeforeNextCharge = boss.Position;
        session.Update(Vector2.Zero, null, dt, width, height);
        var nextChargeStep = boss.Position - posBeforeNextCharge;
        Assert.Equal(550f * dt, nextChargeStep.X, 3);
        Assert.Equal(0f, nextChargeStep.Y, 3);
        Assert.True(charge.IsCharging);
        Assert.NotEmpty(session.Hazards);
        Assert.All(session.Hazards, h => Assert.Equal(HazardKind.Trail, h.Kind));
    }

    [Fact]
    public void GameSession_single_hit_to_ten_percent_health_applies_retune_to_same_movement_instance()
    {
        const float width = 3000f;
        const float height = 1000f;

        var shipPos = new Vector2(1000f, 400f);
        var session = TestEnemies.QuietSession(shipPos);
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 400f));
        session.AddEnemy(boss);
        var charge = Assert.IsType<ChargeAlongLine>(boss.Movement);

        Assert.Equal(60f, boss.HitPoints);
        Assert.Equal(0, boss.PhasesReached);
        Assert.Equal(400f, charge.ChargeSpeed);
        Assert.Equal(700f, charge.ChargeDistance);

        // One TakeDamage drops full-health Autocomplete (60 HP) to ~10% (6 HP) while staying alive
        boss.TakeDamage(54f);
        Assert.Equal(6f, boss.HitPoints);
        Assert.False(boss.IsDead);
        Assert.True(boss.PhasesReached >= 1);
        Assert.Same(charge, boss.Movement);

        // Lock first segment during telegraph
        session.Update(Vector2.Zero, null, 1.2f, width, height);

        // The 75% retune applied: speed 550, distance 900
        Assert.Equal(550f, charge.ChargeSpeed);
        Assert.Equal(900f, charge.ChargeDistance);
        Assert.Equal(900f, Vector2.Distance(charge.AimStart, charge.AimEnd), 3);
    }

    [Fact]
    public void Autocomplete_killing_blow_enters_no_phase()
    {
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 400f));
        var charge = Assert.IsType<ChargeAlongLine>(boss.Movement);

        // A killing blow enters no phase
        boss.TakeDamage(60f);
        Assert.True(boss.IsDead);
        Assert.Equal(0, boss.PhasesReached);
        Assert.Equal(400f, charge.ChargeSpeed);
        Assert.Equal(700f, charge.ChargeDistance);
    }
}
