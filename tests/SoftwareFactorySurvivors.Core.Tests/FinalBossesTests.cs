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
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [new BossArrival(0f, EnemyRoster.GeminiBoss)]);
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
        session.Stats.Damage = 1000f;

        // Position boss directly in front of the ship's firing line
        // Ship starts at Center facing right (heading 0). Boss placed right in front of ship.
        var initialLevel = session.Leveling.Level;
        var initialExp = session.Leveling.Experience;

        // Calculate expected level and experience after adding 60 XP
        var expectedLeveling = new Leveling(initialLevel);
        var expectedLevelsGained = expectedLeveling.Add(60);

        // Advance a frame aiming at boss (heading facing (1,0))
        session.Update(Vector2.Zero, Center + new Vector2(100, 0), 1f / 60f, Width, Height);

        // Wait until laser resolves hit and kills boss
        for (var i = 0; i < 30 && !boss.IsDead; i++)
        {
            session.Update(Vector2.Zero, Center + new Vector2(100, 0), 1f / 60f, Width, Height);
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

        // Advance past 1.2s
        pos = pattern.Move(pos, playerPos, 0.1f);
        // Now it enters charge phase and starts moving
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
            var nextPos = pattern.Move(pos, new Vector2(999, 999), 0.05f); // player moves elsewhere; charge must ignore it!
            pos = nextPos;

            // Assert position lies on the locked segment within epsilon
            var fromStart = pos - lockedStart;
            var projLength = Vector2.Dot(fromStart, segmentDir);
            var projected = lockedStart + segmentDir * projLength;

            Assert.True(Vector2.Distance(pos, projected) < 0.01f, $"Position {pos} deviated from line");
            Assert.True(projLength >= -0.01f && projLength <= totalLength + 0.01f);
        }

        // Verify the line remained the telegraphed one during the charge
        Assert.Equal(lockedStart, pattern.LockedSegment.Start);
        Assert.Equal(lockedEnd, pattern.LockedSegment.End);
    }

    [Fact]
    public void Ship_on_fresh_trail_segment_after_arming_costs_hit_point_and_ends_game_when_exhausted()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 2;

        var trail = Hazard.CreateTrail(Center - new Vector2(50, 0), Center + new Vector2(50, 0), halfWidth: 25f, lifetimeSeconds: 4f, armSeconds: 1f);
        session.AddHazard(trail);

        // Before arming (e.g. at 0.5s), no damage
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.Equal(2, session.Stats.HitPoints);
        Assert.False(session.IsGameOver);

        // Advance to 1.05s (now armed): takes 1 damage, invulnerability granted
        session.Update(Vector2.Zero, null, 0.55f, Width, Height);
        Assert.Equal(1, session.Stats.HitPoints);
        Assert.False(session.IsGameOver);
        Assert.True(session.InvulnerableSecondsLeft > 0f);

        // Advance while invulnerable: no further damage
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.Equal(1, session.Stats.HitPoints);

        // Wait until invulnerability expires (at total time 1.05 + 0.5 + 0.6 = 2.15s < 4.0s lifetime)
        session.Update(Vector2.Zero, null, 0.6f, Width, Height);
        // Ship takes second hit, reducing HitPoints to 0 and triggering GameOver
        Assert.Equal(0, session.Stats.HitPoints);
        Assert.True(session.IsGameOver);
    }

    [Fact]
    public void Ship_off_trail_segment_takes_no_damage()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 3;

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
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 5;

        var trail = Hazard.CreateTrail(Center - new Vector2(50, 0), Center + new Vector2(50, 0), halfWidth: 25f, lifetimeSeconds: 2.0f, armSeconds: 1.0f);
        session.AddHazard(trail);

        // Advance 1.1s: armed, ship takes 1 damage
        session.Update(Vector2.Zero, null, 1.1f, Width, Height);
        Assert.Equal(4, session.Stats.HitPoints);
        Assert.Contains(trail, session.Hazards);

        // Advance past lifetime (another 1.0s, total 2.1s > 2.0s):
        session.Update(Vector2.Zero, null, 1.0f, Width, Height);
        Assert.DoesNotContain(trail, session.Hazards);
        Assert.Empty(session.Hazards);

        // Advance further at the same position, invulnerability now gone: no further damage taken
        session.Update(Vector2.Zero, null, 1.5f, Width, Height);
        Assert.Equal(4, session.Stats.HitPoints);
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
            session.Stats.MaxHitPoints = session.Stats.HitPoints = 3;

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
            Assert.Equal(3, session.Stats.HitPoints);

            session.Update(Vector2.Zero, null, 0.5f, Width, Height); // total 0.9s < 1.0s
            Assert.False(hazard.IsArmed);
            Assert.Equal(3, session.Stats.HitPoints);

            // Advance past 1.0s (total 1.05s) -> now armed and hits
            session.Update(Vector2.Zero, null, 0.15f, Width, Height);
            Assert.True(hazard.IsArmed);
            Assert.Equal(2, session.Stats.HitPoints);
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

        // Enters charge phase (at 1.2s+) and moves:
        session.Update(Vector2.Zero, null, 0.2f, Width, Height);
        Assert.NotEmpty(session.Hazards);
        Assert.All(session.Hazards, h => Assert.Equal(HazardKind.Trail, h.Kind));
    }
}
