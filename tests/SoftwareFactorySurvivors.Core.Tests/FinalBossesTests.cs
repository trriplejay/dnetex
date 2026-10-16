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
}
