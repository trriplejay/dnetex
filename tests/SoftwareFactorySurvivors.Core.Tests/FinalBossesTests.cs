using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class FinalBossesTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void Boss_enemy_types_have_expected_experience_values()
    {
        Assert.Equal(60, EnemyRoster.GeminiBoss.Experience);
        Assert.Equal(120, EnemyRoster.Autocomplete.Experience);
        Assert.Equal(180, EnemyRoster.Hallucination.Experience);
        Assert.Equal(250, EnemyRoster.ClaudeBoss.Experience);
    }

    [Fact]
    public void Killing_gemini_boss_in_session_adds_60_to_leveling_progress()
    {
        // Setup session with GeminiBoss and no other enemies
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [new BossArrival(0f, EnemyRoster.GeminiBoss)]).WithLaser();
        session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        var boss = Assert.Single(session.Bosses);
        Assert.Same(EnemyRoster.GeminiBoss, boss.Type);
        Assert.Equal(60, boss.Experience);

        // Strip shields so boss can take damage
        foreach (var shield in boss.Shields)
        {
            shield.TakeDamage(1000f);
        }

        // Set high laser damage to kill in one hit
        session.Stats.Laser().Damage = 1000f;

        // Position boss directly in front of the ship's firing line
        // Ship starts at Center facing right (heading 0). Boss placed right in front of ship.
        var initialLevel = session.Leveling.Level;
        var initialExp = session.Leveling.Experience;

        // Calculate expected level and experience after adding 60 XP
        var expectedLeveling = new Leveling(initialLevel);
        var expectedLevelsGained = expectedLeveling.Add(60);

        // Advance a frame aiming at boss
        session.Stats.Laser().AttacksPerSecond = 60f;
        session.Update(Vector2.Zero, boss.Position, 1f / 60f, Width, Height);

        // Wait until laser resolves hit and kills boss. The boss arrives off the top edge ~540 px away and
        // lasers fly at 600 px/s, so allow up to 2 s of frames for the shot to land.
        for (var i = 0; i < 120 && !boss.IsDead; i++)
        {
            session.Update(Vector2.Zero, boss.Position, 1f / 60f, Width, Height);
        }

        Assert.True(boss.IsDead);
        Assert.DoesNotContain(boss, session.Bosses);
        Assert.Equal(initialLevel + expectedLevelsGained, session.Leveling.Level);
        Assert.Equal(expectedLeveling.Experience, session.Leveling.Experience);
    }

    [Fact]
    public void ChargeAlongLine_aim_line_exposed_for_at_least_1_second_before_any_displacement()
    {
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: 400f, chargeDistance: 500f);
        var startPos = new Vector2(100, 100);
        var playerPos = new Vector2(300, 100);

        var pos = pattern.Move(startPos, playerPos, 0.1f);
        Assert.True(pattern.IsTelegraphing);
        Assert.False(pattern.IsCharging);
        Assert.Equal(startPos, pos);
        Assert.Equal(startPos, pattern.AimStart);
        Assert.Equal(playerPos, pattern.AimEnd);

        // Advance 0.8 seconds (total 0.9s < 1.0s, and < 1.2s telegraph)
        pos = pattern.Move(pos, playerPos, 0.8f);
        Assert.True(pattern.IsTelegraphing);
        Assert.Equal(startPos, pos);

        // Advance 0.25 seconds (total 1.15s, still telegraphing since telegraphSeconds is 1.2s)
        pos = pattern.Move(pos, playerPos, 0.25f);
        Assert.True(pattern.IsTelegraphing);
        Assert.Equal(startPos, pos);

        // Advance past 1.2s (reaches charge state on the transition frame, no displacement returned yet)
        pos = pattern.Move(pos, playerPos, 0.1f);
        Assert.True(pattern.IsCharging);
        Assert.False(pattern.IsTelegraphing);
        Assert.Equal(startPos, pos);

        // Advance one more frame past the charge start: now it moves along the segment
        pos = pattern.Move(pos, playerPos, 0.05f);
        Assert.True(pattern.IsCharging);
        Assert.False(pattern.IsTelegraphing);
        Assert.True(Vector2.Distance(startPos, pos) > 0f);
    }

    [Fact]
    public void ChargeAlongLine_every_sampled_charge_position_lies_on_locked_segment_and_equals_telegraphed_line()
    {
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.0f, chargeSpeed: 500f, chargeDistance: 400f);
        var startPos = new Vector2(50, 80);
        var playerPos = new Vector2(250, 180);

        // Telegraph phase
        pattern.Move(startPos, playerPos, 0.5f);
        var lockedStart = pattern.AimStart;
        var lockedEnd = pattern.AimEnd;
        Assert.Equal(startPos, lockedStart);
        Assert.Equal(playerPos, lockedEnd);

        // Complete telegraph
        var pos = pattern.Move(startPos, playerPos, 0.5f);
        Assert.Equal(startPos, pos);

        // Sample throughout the charge phase
        var segmentDir = Vector2.Normalize(lockedEnd - lockedStart);
        var totalLength = Vector2.Distance(lockedStart, lockedEnd);

        while (pattern.IsCharging)
        {
            // LockedSegment during charge equals the captured segment
            Assert.Equal(lockedStart, pattern.LockedSegment.Start);
            Assert.Equal(lockedEnd, pattern.LockedSegment.End);

            var nextPos = pattern.Move(pos, new Vector2(999, 999), 0.05f); // player moves elsewhere; charge must ignore it!
            pos = nextPos;

            // Assert position lies on the locked segment within epsilon
            var fromStart = pos - lockedStart;
            var projLength = Vector2.Dot(fromStart, segmentDir);
            var projected = lockedStart + segmentDir * projLength;

            Assert.True(Vector2.Distance(pos, projected) < 0.01f, $"Position {pos} deviated from line");
            Assert.True(projLength >= -0.01f && projLength <= totalLength + 0.01f);
        }

        // Charge is complete, boss reached the end of the segment
        Assert.True(Vector2.Distance(pos, lockedEnd) < 0.01f, $"End position {pos} deviated from lockedEnd {lockedEnd}");
    }

    [Fact]
    public void ChargeAlongLine_far_player_beyond_charge_distance_clamps_aim_line_and_stops_at_segment_end()
    {
        const float chargeDistance = 300f;
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.0f, chargeSpeed: 600f, chargeDistance: chargeDistance);
        var startPos = new Vector2(100, 100);
        var playerPos = new Vector2(1000, 100); // 900px away, well beyond 300px

        // Telegraph phase
        pattern.Move(startPos, playerPos, 0.5f);
        var lockedStart = pattern.AimStart;
        var lockedEnd = pattern.AimEnd;

        Assert.Equal(startPos, lockedStart);
        var segmentLength = Vector2.Distance(lockedStart, lockedEnd);
        Assert.True(segmentLength <= chargeDistance + 0.01f);
        Assert.True(MathF.Abs(segmentLength - chargeDistance) < 0.01f);
        Assert.Equal(startPos + new Vector2(chargeDistance, 0), lockedEnd);

        // Complete telegraph (1.0s total)
        var pos = pattern.Move(startPos, playerPos, 0.5f);
        Assert.Equal(startPos, pos);
        Assert.True(pattern.IsCharging);

        while (pattern.IsCharging)
        {
            Assert.Equal(lockedStart, pattern.LockedSegment.Start);
            Assert.Equal(lockedEnd, pattern.LockedSegment.End);
            pos = pattern.Move(pos, new Vector2(999, 999), 0.05f);
        }

        // Boss position at charge end equals segment end within epsilon
        Assert.True(Vector2.Distance(pos, lockedEnd) < 0.01f, $"Boss position {pos} did not reach segment end {lockedEnd}");
    }

    [Fact]
    public void Ship_on_armed_trail_loses_four_times_the_time_scale_each_tick_and_the_trail_stays_until_hit_points_run_out()
    {
        var session = TestEnemies.QuietSession(Center);

        var trail = Hazard.CreateTrail(Center - new Vector2(50, 0), Center + new Vector2(50, 0), halfWidth: 25f, lifetimeSeconds: 60f, armSeconds: 1f);
        session.AddHazard(trail);
        Assert.Equal(4f, trail.ContactDamage);

        // Before arming (e.g. at 0.5s), no damage
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.Equal(100f, session.Stats.HitPoints);

        // Advance to 1.05s (now armed): one tick of 4 x scale, no immunity, the trail stays.
        session.Update(Vector2.Zero, null, 0.55f, Width, Height);
        var expected = 100f - EnemyRoster.ScaledContactDamage(Hazard.TrailContactDamage, session.ElapsedSeconds);
        Assert.Equal(expected, session.Stats.HitPoints, precision: 2);
        Assert.Equal(0f, session.InvulnerableSecondsLeft);
        Assert.Contains(trail, session.Hazards);

        // Every further 0.5 s on the trail costs another tick, scaled for its moment.
        for (var tick = 0; tick < 3; tick++)
        {
            session.Update(Vector2.Zero, null, 0.25f, Width, Height);
            Assert.Equal(expected, session.Stats.HitPoints, precision: 2);
            session.Update(Vector2.Zero, null, 0.25f, Width, Height);
            expected -= EnemyRoster.ScaledContactDamage(Hazard.TrailContactDamage, session.ElapsedSeconds);
            Assert.Equal(expected, session.Stats.HitPoints, precision: 2);
            Assert.Contains(trail, session.Hazards);
        }

        // Stay on it: hit points run out, are stored as exactly 0, and the game is lost.
        for (var i = 0; i < 400 && !session.IsGameOver; i++)
            session.Update(Vector2.Zero, null, 0.05f, Width, Height);
        Assert.True(session.IsGameOver);
        Assert.Equal(0f, session.Stats.HitPoints);
    }

    [Fact]
    public void Ship_off_trail_segment_takes_no_damage()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 3f;
        session.Stats.HitPoints = 3f;

        var trail = Hazard.CreateTrail(new Vector2(100, 100), new Vector2(200, 100), halfWidth: 25f, lifetimeSeconds: 4f, armSeconds: 1f);
        session.AddHazard(trail);

        // Advance past arming time
        session.Update(Vector2.Zero, null, 2.0f, Width, Height);

        Assert.Equal(3, session.Stats.HitPoints);
        Assert.False(session.IsGameOver);
    }

    [Fact]
    public void Trail_segment_expires_after_lifetime_and_no_longer_damages()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 5f;
        session.Stats.HitPoints = 5f;

        var trail = Hazard.CreateTrail(Center - new Vector2(50, 0), Center + new Vector2(50, 0), halfWidth: 25f, lifetimeSeconds: 2.0f, armSeconds: 1.0f);
        session.AddHazard(trail);

        // Advance 1.1s: armed, ship takes one trail tick
        session.Update(Vector2.Zero, null, 1.1f, Width, Height);
        var afterTick = 5f - EnemyRoster.ScaledContactDamage(Hazard.TrailContactDamage, 1.1f);
        Assert.Equal(afterTick, session.Stats.HitPoints, precision: 2);
        Assert.Contains(trail, session.Hazards);

        // Advance past lifetime (another 1.0s, total 2.1s > 2.0s):
        session.Update(Vector2.Zero, null, 1.0f, Width, Height);
        Assert.DoesNotContain(trail, session.Hazards);
        Assert.Empty(session.Hazards);

        // Advance further at the same position: the expired trail deals no further damage
        session.Update(Vector2.Zero, null, 1.5f, Width, Height);
        Assert.Equal(afterTick, session.Stats.HitPoints, precision: 2);
    }

    [Fact]
    public void Hazard_arm_time_guard_enforces_at_least_one_second_for_all_hazard_kinds_and_zero_warmup_fixture()
    {
        // Guard discriminates: requested with zero warmup is still clamped to >= 1.0s
        var zeroWarmupFixture = new Hazard(
            HazardKind.Trail,
            new ProjectileShape([new ThickLine(Center, Center + new Vector2(10, 0), 20f)], [], []),
            lifetimeSeconds: 5f,
            armSeconds: 0f);
        Assert.Equal(1.0f, zeroWarmupFixture.ArmSeconds);
        Assert.False(zeroWarmupFixture.IsArmed);

        // Enumerate every hazard kind: Trail, Ring, Bullet
        var kinds = new[] { HazardKind.Trail, HazardKind.Ring, HazardKind.Bullet };
        foreach (var kind in kinds)
        {
            var session = TestEnemies.QuietSession(Center);
            session.Stats.MaxHitPoints = 30f;
            session.Stats.HitPoints = 30f;

            Hazard hazard = kind switch
            {
                HazardKind.Trail => Hazard.CreateTrail(Center - new Vector2(10, 0), Center + new Vector2(10, 0), halfWidth: 25f, armSeconds: 0f),
                HazardKind.Ring => Hazard.CreateRing(Center, radius: 50f, armSeconds: 0f),
                HazardKind.Bullet => Hazard.CreateBullet(Center, radius: 10f, armSeconds: 0f),
                _ => throw new InvalidOperationException()
            };

            Assert.True(hazard.ArmSeconds >= 1.0f);
            session.AddHazard(hazard);

            // Placed on ship at creation, asserts no hit before 1.0s
            session.Update(Vector2.Zero, null, 0.4f, Width, Height);
            Assert.False(hazard.IsArmed);
            Assert.Equal(30f, session.Stats.HitPoints);

            session.Update(Vector2.Zero, null, 0.5f, Width, Height); // total 0.9s < 1.0s
            Assert.False(hazard.IsArmed);
            Assert.Equal(30f, session.Stats.HitPoints);

            // Advance past 1.0s (total 1.05s) -> now armed and hits for its kind's damage, scaled for the clock
            session.Update(Vector2.Zero, null, 0.15f, Width, Height);
            Assert.True(hazard.IsArmed);
            Assert.Equal(30f - EnemyRoster.ScaledContactDamage(Hazard.DefaultContactDamage(kind), session.ElapsedSeconds),
                session.Stats.HitPoints, precision: 2);
        }
    }

    [Fact]
    public void Autocomplete_charge_leaves_trail_hazard_segments_covering_charge_path()
    {
        var session = TestEnemies.QuietSession(Center);
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100, 400));
        session.AddEnemy(boss);

        // Autocomplete telegraphs for 1.2s: no hazard yet
        session.Update(Vector2.Zero, null, 1.1f, Width, Height);
        Assert.Empty(session.Hazards);

        // Enters charge phase (at 1.2s+) on the transition frame (no displacement returned yet):
        session.Update(Vector2.Zero, null, 0.2f, Width, Height);

        // Advance one more frame past the charge's start to move along the segment:
        session.Update(Vector2.Zero, null, 0.1f, Width, Height);
        Assert.NotEmpty(session.Hazards);
        Assert.All(session.Hazards, h => Assert.Equal(HazardKind.Trail, h.Kind));
    }

    [Fact]
    public void Only_ClaudeBoss_produces_bullet_hazards_across_all_roster_and_boss_enemy_types()
    {
        var allTypes = EnemyRoster.All
            .Concat([EnemyRoster.GeminiBoss, EnemyRoster.Autocomplete, EnemyRoster.Hallucination, EnemyRoster.ClaudeBoss])
            .Distinct();

        foreach (var type in allTypes)
        {
            var ability = type.CreateAbility?.Invoke();
            if (type == EnemyRoster.ClaudeBoss)
            {
                Assert.NotNull(ability);
                Assert.IsType<CompositeAbility>(ability);
                var comp = (CompositeAbility)ability;
                Assert.Contains(comp.Abilities, a => a is BulletBursts);
            }
            else if (ability is IHazardProducer producer)
            {
                var dummyOwner = new Enemy(type, Center);
                producer.TakeHazards(); // flush any initial
                ability.Update(dummyOwner, Center, 10f, new Random(1));
                var hazards = producer.TakeHazards();
                Assert.DoesNotContain(hazards, h => h.Kind == HazardKind.Bullet);
            }
            else if (ability is CompositeAbility composite)
            {
                Assert.DoesNotContain(composite.Abilities, a => a is BulletBursts);
            }
        }
    }

    [Fact]
    public void ClaudeBoss_in_GameSession_yields_ring_and_bullet_hazards_unarmed_in_first_one_second()
    {
        var session = TestEnemies.QuietSession(Center);
        // A finished session stops advancing ElapsedSeconds; keep the ship alive so the wait loop below terminates.
        // Hold the boss still: an added (non-arrival) boss rammed by its Dash is destroyed on contact before
        // BulletBursts' first burst at 3 s, which is not what this hazard test is about.
        session.Stats.MaxHitPoints = 1_000_000f;
        session.Stats.HitPoints = 1_000_000f;
        var claudeBoss = TestEnemies.Stationary(EnemyRoster.ClaudeBoss, new Vector2(200, 200));
        session.AddEnemy(claudeBoss);

        // Advance 0.5 seconds
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);

        // ClosingRing spawns a ring hazard on its first tick; BulletBursts emits bullets.
        // Wait up to 3.5 seconds in small steps so BulletBursts triggers its first burst (default cooldown 3.0s).
        while (!session.Hazards.Any(h => h.Kind == HazardKind.Bullet) && session.ElapsedSeconds < 4.0f)
        {
            session.Update(Vector2.Zero, null, 0.1f, Width, Height);
        }

        Assert.Contains(session.Hazards, h => h.Kind == HazardKind.Ring);
        Assert.Contains(session.Hazards, h => h.Kind == HazardKind.Bullet);

        // Every hazard present within its first 1.0 second of age must be unarmed
        foreach (var hazard in session.Hazards)
        {
            if (hazard.AgeSeconds < 1.0f)
            {
                Assert.False(hazard.IsArmed, $"Hazard of kind {hazard.Kind} with age {hazard.AgeSeconds} was unexpectedly armed.");
            }
        }
    }

    [Fact]
    public void Hallucination_boss_in_GameSession_produces_copies_after_split_interval()
    {
        var session = TestEnemies.QuietSession(new Vector2(900, 700));
        session.Stats.MaxHitPoints = 50f;
        session.Stats.HitPoints = 50f;
        var hallucination = new Enemy(EnemyRoster.Hallucination, new Vector2(200, 200));
        session.AddEnemy(hallucination);

        Assert.Single(session.Enemies);
        Assert.Empty(session.Enemies.Where(e => e.IsCopy));

        // Advance less than split interval (e.g. 2.0s < 5.0s)
        for (var i = 0; i < 20; i++)
        {
            session.Update(Vector2.Zero, null, 0.1f, Width, Height);
        }
        Assert.False(session.IsGameOver);
        Assert.Empty(session.Enemies.Where(e => e.IsCopy));

        // Advance past 5.0s split interval (another 3.5s = 5.5s total)
        for (var i = 0; i < 35; i++)
        {
            session.Update(Vector2.Zero, null, 0.1f, Width, Height);
        }

        Assert.False(session.IsGameOver);
        Assert.Contains(hallucination, session.Enemies);
        Assert.False(hallucination.IsDead);

        var copies = session.Enemies.Where(e => e.IsCopy).ToList();
        Assert.NotEmpty(copies);
        Assert.Equal(2, copies.Count);
        Assert.All(copies, c =>
        {
            Assert.True(c.IsCopy);
            Assert.Null(c.Ability);
            Assert.Same(EnemyRoster.Hallucination, c.Type);
            Assert.Equal(hallucination.Type.Radius, c.Type.Radius);
            Assert.Equal(0, c.Experience);
        });
        Assert.Equal(3, session.Enemies.Count);
    }

    [Fact]
    public void Hallucination_copies_have_no_ability_and_do_not_split_further_over_multiple_periods()
    {
        var session = TestEnemies.QuietSession(new Vector2(2500, 2500));
        session.Stats.MaxHitPoints = 50f;
        session.Stats.HitPoints = 50f;
        var boss = new Enemy(EnemyRoster.Hallucination, new Vector2(300, 300));
        session.AddEnemy(boss);

        // Advance 3 split periods (15 seconds total)
        for (var i = 0; i < 160; i++)
        {
            session.Update(Vector2.Zero, null, 0.1f, Width, Height);
        }

        Assert.False(session.IsGameOver);
        var copies = session.Enemies.Where(e => e.IsCopy).ToList();
        // The real boss splits 3 times producing 2 copies each = 6 copies total.
        // Copies themselves never split because their Ability is null.
        Assert.Equal(6, copies.Count);
        Assert.All(copies, c => Assert.Null(c.Ability));
    }

    [Fact]
    public void Closing_ring_interior_exterior_and_disarmed_band_cost_nothing()
    {
        var session = TestEnemies.QuietSession(Center);

        // Ship held strictly inside a contracting ring for its whole life: nothing.
        var inside = ClosingRing.CreateRingHazard(Center, initialRadius: 200f, minRadius: 70f, contractionSpeed: 40f,
            warmupSeconds: 1.0f, lifetimeSeconds: 5.0f);
        session.AddHazard(inside);
        for (var t = 0f; t < 5.5f; t += 0.5f)
        {
            session.Update(Vector2.Zero, null, 0.5f, Width, Height);
            Assert.Equal(100f, session.Stats.HitPoints);
        }
        Assert.DoesNotContain(inside, session.Hazards);

        // Ship strictly outside an armed ring: nothing.
        var outside = ClosingRing.CreateRingHazard(Center + new Vector2(400, 0), initialRadius: 80f, minRadius: 60f,
            warmupSeconds: 1.0f, lifetimeSeconds: 3.0f);
        session.AddHazard(outside);
        session.Update(Vector2.Zero, null, 1.5f, Width, Height);
        Assert.True(outside.IsArmed);
        Assert.Equal(100f, session.Stats.HitPoints);

        // Ship on the band of a ring still warming up: nothing until it arms.
        var onBand = ClosingRing.CreateRingHazard(Center + new Vector2(100, 0), initialRadius: 100f, minRadius: 100f,
            contractionSpeed: 0f, warmupSeconds: 1.0f, lifetimeSeconds: 5.0f);
        session.AddHazard(onBand);
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.False(onBand.IsArmed);
        Assert.Equal(100f, session.Stats.HitPoints);
    }

    [Fact]
    public void Boss_on_the_ship_survives_and_deals_fifteen_times_the_time_scale_each_tick()
    {
        var session = TestEnemies.QuietSession(Center);
        var boss = TestEnemies.Stationary(EnemyRoster.Hallucination, Center);
        session.AddEnemy(boss);
        Assert.Equal(15f, EnemyRoster.Hallucination.ContactDamage);

        var expected = 100f;
        for (var tick = 0; tick < 4; tick++)
        {
            session.Update(Vector2.Zero, null, tick == 0 ? 0.01f : 0.5f, Width, Height);
            expected -= EnemyRoster.ScaledContactDamage(EnemyRoster.BossContactDamage, session.ElapsedSeconds);
            Assert.Equal(expected, session.Stats.HitPoints, precision: 2);
            Assert.Contains(boss, session.Enemies);
        }
        Assert.False(boss.IsDead);
    }

    [Fact]
    public void Hazard_produced_during_frame_does_not_age_until_subsequent_updates_and_no_hit_before_cumulative_one_second()
    {
        // Realistic dt = 1/60s (approx 0.016667s)
        const float dt = 1f / 60f;
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 50f;
        session.Stats.HitPoints = 50f;

        // Create an armed-guarded hazard touching the ship at Center with default clamped armSeconds = 1.0s
        var trail = Hazard.CreateTrail(Center - new Vector2(20, 0), Center + new Vector2(20, 0), halfWidth: 25f, armSeconds: 1.0f);
        session.AddHazard(trail);

        // Frame 0: The hazard was added directly (simulating appearing during this frame).
        // During this frame's Update, existing hazards are aged by dt (so AgeSeconds becomes dt).
        // It is not yet armed because AgeSeconds (dt ~ 0.0167s) < ArmSeconds (1.0s), so no hit occurs.
        session.Update(Vector2.Zero, null, dt, Width, Height);
        Assert.Equal(dt, trail.AgeSeconds, precision: 4);
        Assert.False(trail.IsArmed);
        Assert.Equal(50f, session.Stats.HitPoints);

        // Step frame-by-frame with dt = 1/60s. For every update where cumulative time since hazard appeared is < 1.0s,
        // no hit may occur.
        // Frames 1 through 59: total elapsed time = (frame + 1) * dt.
        // At frame 58 (59 total frames), elapsed time is 59/60s = ~0.9833s < 1.0s.
        for (var frame = 1; frame < 59; frame++)
        {
            session.Update(Vector2.Zero, null, dt, Width, Height);
            var totalTime = (frame + 1) * dt;
            Assert.False(trail.IsArmed, $"Trail hazard was armed at cumulative time {totalTime:F4}s (< 1.0s)");
            Assert.Equal(50f, session.Stats.HitPoints);
        }

        // Step clearly past 1.0s (e.g. two frames of dt, reaching 61/60s = ~1.0167s) so float accumulation
        // roundoff does not stop at 0.9999997s. Now it becomes armed and deals exactly one hit.
        session.Update(Vector2.Zero, null, dt * 2, Width, Height);
        Assert.True(trail.IsArmed);
        Assert.Equal(50f - EnemyRoster.ScaledContactDamage(Hazard.TrailContactDamage, session.ElapsedSeconds),
            session.Stats.HitPoints, precision: 2);
    }

    [Fact]
    public void Directly_built_ring_with_aggressive_contraction_never_drops_below_minimum_radius_and_center_ship_stays_safe()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 5f;
        session.Stats.HitPoints = 5f;

        // Create ring directly with parameters that would contract well below DefaultMinRadius (60f)
        // e.g. initial radius 100, contractionSpeed 100 px/s over 3 seconds -> unconstrained would reach -200px or tiny minRadius.
        // Requested minRadius is 10px (< ship diameter ~48px), which must clamp to >= DefaultMinRadius (60px).
        const float requestedMinRadius = 10f;
        const float initialRadius = 100f;
        const float contractionSpeed = 100f;
        const float lifetimeSeconds = 3.0f;
        const float warmupSeconds = 1.0f;

        var hazard = ClosingRing.CreateRingHazard(
            Center,
            initialRadius: initialRadius,
            minRadius: requestedMinRadius,
            contractionSpeed: contractionSpeed,
            warmupSeconds: warmupSeconds,
            lifetimeSeconds: lifetimeSeconds);

        session.AddHazard(hazard);

        // Ship is held at Center (inside the ring).
        // Advance step-by-step through its entire lifetime, sampling the ring radius each step.
        const float dt = 0.1f;
        for (var t = 0f; t < lifetimeSeconds; t += dt)
        {
            session.Update(Vector2.Zero, null, dt, Width, Height);

            // Sample the radius of the ring segments
            Assert.NotEmpty(hazard.Shape.Lines);
            var currentRadius = Vector2.Distance(Center, hazard.Shape.Lines[0].A);

            // Radius must never drop below DefaultMinRadius (60px), which is strictly greater than ship diameter (48px based on Ship strokes [-24, 24])
            const float shipDiameter = 48f;
            Assert.True(currentRadius >= ClosingRing.DefaultMinRadius,
                $"Ring radius {currentRadius} dropped below DefaultMinRadius {ClosingRing.DefaultMinRadius} at t={t:F1}s");
            Assert.True(currentRadius > shipDiameter,
                $"Ring radius {currentRadius} dropped below ship diameter {shipDiameter} at t={t:F1}s");

            // Ship held at Center must remain completely untouched throughout the ring's whole life
            Assert.Equal(5, session.Stats.HitPoints);
        }

        // After its lifetime expires, ship still untouched
        Assert.DoesNotContain(hazard, session.Hazards);
        Assert.Equal(5, session.Stats.HitPoints);
    }

    [Fact]
    public void Killing_real_hallucination_boss_with_weapon_hits_removes_all_copies_and_grants_only_boss_experience()
    {
        var session = new GameSession(
            Center,
            new Random(1),
            roster: [],
            bosses: [new BossArrival(0f, EnemyRoster.Hallucination)],
            rings: []).WithLaser();
        session.Stats.MaxHitPoints = 50f;
        session.Stats.HitPoints = 50f;

        // Advance 1 frame so boss arrives
        session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
        var boss = Assert.Single(session.Bosses);
        Assert.Same(EnemyRoster.Hallucination, boss.Type);
        Assert.Equal(180, boss.Experience);

        // Hold fire while waiting for the split. FiringTimer rejects a rate of 0 and any hit kills a copy,
        // so use a positive rate too slow to fire a single shot within the wait.
        session.Stats.Laser().AttacksPerSecond = 1e-6f;

        // Advance past split interval (5.0s, e.g. 5.5s) to allow it to spawn copies
        for (var i = 0; i < 55; i++)
        {
            session.Update(Vector2.Zero, null, 0.1f, Width, Height);
        }

        var copies = session.Enemies.Where(e => e.IsCopy).ToList();
        Assert.NotEmpty(copies);
        Assert.Equal(2, copies.Count);
        Assert.All(copies, c => Assert.False(c.IsDead));
        Assert.False(boss.IsDead);
        Assert.Contains(boss, session.Bosses);

        var initialLevel = session.Leveling.Level;
        var initialExp = session.Leveling.Experience;
        var expectedLeveling = new Leveling(initialLevel);
        var expectedLevelsGained = expectedLeveling.Add(180);

        // Turn on high weapon damage to kill with weapon hits
        session.Stats.Laser().Damage = 1000f;
        session.Stats.Laser().AttacksPerSecond = 60f;

        var copiesAliveBeforeBossDeath = 0;
        for (var i = 0; i < 60 && !boss.IsDead; i++)
        {
            copiesAliveBeforeBossDeath = session.Enemies.Count(e => e.IsCopy && !e.IsDead);
            session.Update(Vector2.Zero, boss.Position, 1f / 60f, Width, Height);
        }

        Assert.True(boss.IsDead);
        Assert.True(copiesAliveBeforeBossDeath > 0, "At least one copy must be alive before the real boss death to verify reaping.");
        Assert.DoesNotContain(boss, session.Bosses);
        Assert.Empty(session.Bosses);

        // After that Update, all copies must be removed from enemies
        Assert.Empty(session.Enemies.Where(e => e.IsCopy));
        Assert.Empty(session.Enemies);

        // Experience gained equals only the boss's 180 XP
        Assert.Equal(initialLevel + expectedLevelsGained, session.Leveling.Level);
        Assert.Equal(expectedLeveling.Experience, session.Leveling.Experience);
    }
}
