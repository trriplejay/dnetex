using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class DashCreepFloorTests
{
    [Fact]
    public void Dash_floors_creep_seconds_at_one_second_for_sub_one_inputs()
    {
        // creepSeconds 0.2, stepped at 1/64 s
        var dash = new Dash(creepSpeed: 100f, dashSpeed: 500f, creepSeconds: 0.2f, dashSeconds: 0.5f);
        Assert.Equal(1.0f, dash.CreepSeconds);

        var playerPos = new Vector2(1000f, 0f);
        var bossPos = Vector2.Zero;
        const float dt = 1f / 64f;

        // At 1/64 s = 0.015625s per tick:
        // 63 ticks = 0.984375s (< 1.0s) -> must be creep
        // 64 ticks = 1.000000s (>= 1.0s) -> lunge triggers on this tick
        for (int step = 0; step < 63; step++)
        {
            var nextPos = dash.Move(bossPos, playerPos, dt);
            var stepDist = Vector2.Distance(bossPos, nextPos);
            // In creep, stepDist == 100 * (1/64) = 1.5625. Lunge would be 500 * (1/64) = 7.8125.
            Assert.True(stepDist <= 100f * dt + 0.001f,
                $"Expected creep on tick {step + 1} (time={(step + 1) * dt:F4}s), but got displacement {stepDist}");
            bossPos = nextPos;
        }

        // On tick 64, time reaches 1.0s, so lunge triggers and moves at 500 px/s
        var lungePos = dash.Move(bossPos, playerPos, dt);
        var lungeDist = Vector2.Distance(bossPos, lungePos);
        Assert.True(lungeDist >= 400f * dt,
            $"Expected lunge on tick 64 (time={64 * dt:F4}s), but got displacement {lungeDist}");
    }

    [Fact]
    public void Dash_first_and_second_lunge_both_require_one_second_creep_when_configured_with_sub_one_second()
    {
        var dash = new Dash(creepSpeed: 100f, dashSpeed: 500f, creepSeconds: 0.2f, dashSeconds: 0.4f);
        Assert.Equal(1.0f, dash.CreepSeconds);

        var playerPos = new Vector2(2000f, 0f);
        var bossPos = Vector2.Zero;
        const float dt = 1f / 64f;

        // Track how much creep time accumulates before each lunge starts.
        int lungeIndex = 0;
        float creepTimeInCycle = 0f;
        bool inLunge = false;

        // Step for 4 seconds (256 ticks).
        // With 1.0s creep + 0.4s dash -> ~1.4s per cycle.
        // 256 ticks easily covers 2 full lunges.
        for (int tick = 1; tick <= 256; tick++)
        {
            var prevPos = bossPos;
            bossPos = dash.Move(bossPos, playerPos, dt);
            float dist = Vector2.Distance(prevPos, bossPos);
            bool isLungeDist = dist > 200f * dt;

            if (isLungeDist)
            {
                if (!inLunge)
                {
                    // Transition to lunge: start of a new lunge!
                    lungeIndex++;
                    // Note that the tick that triggers the lunge also advanced creep time by dt,
                    // so creep accumulated is creepTimeInCycle + dt.
                    float totalCreepBeforeLunge = creepTimeInCycle + dt;
                    Assert.True(totalCreepBeforeLunge >= 1.0f - 1e-4f,
                        $"Lunge #{lungeIndex} started after only {totalCreepBeforeLunge:F4}s of creep, expected >= 1.0s");
                    creepTimeInCycle = 0f;
                    inLunge = true;
                }
            }
            else
            {
                inLunge = false;
                creepTimeInCycle += dt;
            }
        }

        Assert.True(lungeIndex >= 2, $"Expected at least 2 lunges to be tested, got {lungeIndex}");
    }

    [Theory]
    [InlineData(1.2f)]
    [InlineData(1.6f)]
    public void Dash_reports_and_uses_exact_creep_seconds_when_at_or_above_one_second(float expectedCreep)
    {
        var dash = new Dash(creepSpeed: 100f, dashSpeed: 500f, creepSeconds: expectedCreep, dashSeconds: 0.4f);
        Assert.Equal(expectedCreep, dash.CreepSeconds);

        var playerPos = new Vector2(2000f, 0f);
        var bossPos = Vector2.Zero;
        const float dt = 1f / 64f;

        // Number of ticks before expectedCreep:
        // For 1.2s: ceil(1.2 / (1/64)) = ceil(76.8) = 77 ticks
        // Ticks 1..76 have total elapsed time 76/64 = 1.1875s < 1.2s, so all must be creep.
        int requiredTicks = (int)MathF.Ceiling(expectedCreep / dt);

        for (int step = 1; step < requiredTicks; step++)
        {
            var nextPos = dash.Move(bossPos, playerPos, dt);
            float dist = Vector2.Distance(bossPos, nextPos);
            Assert.True(dist <= 100f * dt + 0.001f,
                $"Tick {step} (t={step * dt:F4}s) displaced {dist}, expected creep before {expectedCreep}s");
            bossPos = nextPos;
        }

        // At requiredTicks, total elapsed time >= expectedCreep, so lunge triggers!
        var lungePos = dash.Move(bossPos, playerPos, dt);
        float lungeDist = Vector2.Distance(bossPos, lungePos);
        Assert.True(lungeDist >= 400f * dt,
            $"Tick {requiredTicks} (t={requiredTicks * dt:F4}s) should trigger lunge for creepSeconds={expectedCreep}");
    }

    [Fact]
    public void Retune_changes_lunge_speed_without_affecting_creep()
    {
        var dash = new Dash(creepSpeed: 80f, dashSpeed: 420f, creepSeconds: 1.6f, dashSeconds: 0.45f);
        Assert.Equal(1.6f, dash.CreepSeconds);

        dash.Retune(560f);
        Assert.Equal(1.6f, dash.CreepSeconds);
    }
}
