using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ClaudeBossLungeTests
{
    private const float Step = 1f / 64f;
    private static readonly Vector2 Start = new(0f, 0f);
    // Far away along +x, so every lunge runs straight along x and never reaches the player.
    private static readonly Vector2 Player = new(1_000_000f, 0f);

    private sealed record Lunge(int FirstStep, int LastStep, float Distance);

    /// <summary>Drives the boss one fixed step at a time, recording each lunge as a run of fast steps.</summary>
    private sealed class LungeRecorder(Enemy boss)
    {
        // Creep moves 80 px/s (1.25 px a step); lunges move 420+ px/s (6.5+ px a step).
        private const float LungeStepThreshold = 200f * Step;

        public int StepIndex { get; private set; }
        public List<Lunge> Lunges { get; } = [];
        public bool InLunge { get; private set; }
        private int _first;
        private float _distance;

        public void Run(int steps)
        {
            for (var i = 0; i < steps; i++)
                StepOnce();
        }

        public void StepOnce()
        {
            StepIndex++;
            var before = boss.Position;
            boss.Update(Player, Step);
            var moved = Vector2.Distance(before, boss.Position);
            if (moved > LungeStepThreshold)
            {
                if (!InLunge)
                {
                    InLunge = true;
                    _first = StepIndex;
                    _distance = 0f;
                }
                _distance += moved;
            }
            else if (InLunge)
            {
                InLunge = false;
                Lunges.Add(new Lunge(_first, StepIndex - 1, _distance));
            }
        }

        public void RunUntilLunges(int count)
        {
            var guard = 0;
            while (Lunges.Count < count)
            {
                StepOnce();
                Assert.True(++guard < 100_000, "Boss stopped lunging.");
            }
        }

        public void RunUntilInLunge()
        {
            var guard = 0;
            while (!InLunge)
            {
                StepOnce();
                Assert.True(++guard < 100_000, "Boss stopped lunging.");
            }
        }
    }

    private const float Tolerance = 560f * Step + 0.5f;

    private static void AssertLunge(Lunge lunge, float speed) =>
        Assert.InRange(lunge.Distance, speed * 0.45f - Tolerance, speed * 0.45f + Tolerance);

    private static void AssertCreepGap(Lunge previous, Lunge next, float creepSeconds = 1.6f)
    {
        var gap = (next.FirstStep - previous.LastStep) * Step;
        Assert.InRange(gap, creepSeconds - 2f * Step, creepSeconds + 2f * Step);
    }

    [Theory]
    [InlineData(false)] // staged: 120 -> 90 (75%) -> 60 (50%)
    [InlineData(true)]  // one hit 120 -> 30, crossing 75%, 50% and 25%
    public void Lunges_after_the_50_percent_crossing_run_at_560_on_the_same_Dash_without_restarting_creep(bool oneHit)
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Start);
        var dash = Assert.IsType<Dash>(boss.Movement);
        var recorder = new LungeRecorder(boss);

        // Before 50%: full lunges at 420 px/s, 1.6 s of creep apart.
        recorder.RunUntilLunges(2);
        AssertLunge(recorder.Lunges[0], 420f);
        AssertLunge(recorder.Lunges[1], 420f);
        AssertCreepGap(recorder.Lunges[0], recorder.Lunges[1]);

        // Cross 50% about 0.8 s into the creep, so a restarted creep would show as a longer gap.
        recorder.Run((int)(0.8f / Step));
        if (oneHit)
        {
            boss.TakeDamage(90f);
            Assert.Equal(3, boss.PhasesReached);
        }
        else
        {
            boss.TakeDamage(30f);
            Assert.Same(dash, boss.Movement);
            boss.TakeDamage(30f);
            Assert.Equal(2, boss.PhasesReached);
        }
        Assert.False(boss.IsDead);
        Assert.Same(dash, boss.Movement);

        recorder.RunUntilLunges(4);
        AssertLunge(recorder.Lunges[2], 560f);
        AssertCreepGap(recorder.Lunges[1], recorder.Lunges[2]);
        AssertLunge(recorder.Lunges[3], 560f);
        AssertCreepGap(recorder.Lunges[2], recorder.Lunges[3]);

        // A later phase keeps the 50% lunge speed.
        if (!oneHit)
        {
            boss.TakeDamage(35f); // 60 -> 25: crosses 25%
            Assert.Equal(3, boss.PhasesReached);
            Assert.Same(dash, boss.Movement);
        }
        recorder.RunUntilLunges(6);
        AssertLunge(recorder.Lunges[4], 560f);
        AssertLunge(recorder.Lunges[5], 560f);
        AssertCreepGap(recorder.Lunges[4], recorder.Lunges[5]);
        Assert.Same(dash, boss.Movement);
    }

    [Fact]
    public void A_lunge_under_way_at_the_50_percent_crossing_finishes_at_420_and_the_next_runs_at_560()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Start);
        var dash = boss.Movement;
        var recorder = new LungeRecorder(boss);

        recorder.RunUntilLunges(1);
        recorder.RunUntilInLunge();
        recorder.Run(5); // part way through the second lunge

        boss.TakeDamage(60f);
        Assert.Equal(2, boss.PhasesReached);
        Assert.Same(dash, boss.Movement);

        recorder.RunUntilLunges(3);
        AssertLunge(recorder.Lunges[1], 420f);
        AssertLunge(recorder.Lunges[2], 560f);
        AssertCreepGap(recorder.Lunges[1], recorder.Lunges[2]);
    }

    [Fact]
    public void Retune_changes_only_the_ClaudeBoss_Dash_instance_leaving_regular_Claude_and_Hallucination_at_420()
    {
        // 1. Regular Claude alive during the crossing
        var liveClaude = new Enemy(EnemyRoster.Claude, Start);
        var liveClaudeRecorder = new LungeRecorder(liveClaude);
        liveClaudeRecorder.RunUntilLunges(1);
        AssertLunge(liveClaudeRecorder.Lunges[0], 420f);

        // 2. ClaudeBoss crosses 0.50 and its lunges retune to 560
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Start);
        var bossRecorder = new LungeRecorder(boss);
        bossRecorder.RunUntilLunges(1);
        AssertLunge(bossRecorder.Lunges[0], 420f);

        boss.TakeDamage(60f); // 120 -> 60 (50% health threshold)
        Assert.Equal(2, boss.PhasesReached);

        bossRecorder.RunUntilLunges(2);
        AssertLunge(bossRecorder.Lunges[1], 560f);

        // 3. Regular Claude alive during the crossing still lunges at 420 with 1.6 s creep gap
        liveClaudeRecorder.RunUntilLunges(2);
        AssertLunge(liveClaudeRecorder.Lunges[1], 420f);
        AssertCreepGap(liveClaudeRecorder.Lunges[0], liveClaudeRecorder.Lunges[1], 1.6f);

        // 4. Regular Claude built after the crossing lunges at 420 with 1.6 s creep gap
        var newClaude = new Enemy(EnemyRoster.Claude, Start);
        var newClaudeRecorder = new LungeRecorder(newClaude);
        newClaudeRecorder.RunUntilLunges(2);
        AssertLunge(newClaudeRecorder.Lunges[0], 420f);
        AssertLunge(newClaudeRecorder.Lunges[1], 420f);
        AssertCreepGap(newClaudeRecorder.Lunges[0], newClaudeRecorder.Lunges[1], 1.6f);

        // 5. Hallucination boss damaged to <= 25% installs Dash and lunges at 420 with 1.2 s creep gap
        var hallucination = new Enemy(EnemyRoster.Hallucination, Start);
        hallucination.TakeDamage(60f); // 80 -> 20 (25% health threshold)
        Assert.Equal(3, hallucination.PhasesReached);
        var hallucinationDash = Assert.IsType<Dash>(hallucination.Movement);
        Assert.Equal(1.2f, hallucinationDash.CreepSeconds);

        var hallucinationRecorder = new LungeRecorder(hallucination);
        hallucinationRecorder.RunUntilLunges(2);
        AssertLunge(hallucinationRecorder.Lunges[0], 420f);
        AssertLunge(hallucinationRecorder.Lunges[1], 420f);
        AssertCreepGap(hallucinationRecorder.Lunges[0], hallucinationRecorder.Lunges[1], 1.2f);
    }
}
