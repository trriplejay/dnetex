using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>How often a split happens and how many copies it makes.</summary>
/// <param name="SecondsBetweenSplits">Interval between splits.</param>
/// <param name="CopiesPerSplit">Number of copies each split makes.</param>
public sealed record SplitStep(float SecondsBetweenSplits, int CopiesPerSplit);

/// <summary>
/// Boss ability that periodically spawns look-alike copy enemies that share the owner's appearance,
/// radius, and movement, but give 0 experience, die in one hit, and never flicker.
/// The schedule can tighten as the owner reaches its phases: the same instance reads the owner's
/// PhasesReached on every update, so the time since the last split carries over a phase change
/// instead of starting again from zero.
/// </summary>
/// <param name="secondsBetweenSplits">Interval between split attempts before the owner reaches any phase.</param>
/// <param name="copiesPerSplit">Number of copies to spawn each split before the owner reaches any phase.</param>
/// <param name="spawnDistance">Distance offset from the owner's position for copies.</param>
/// <param name="phaseSchedule">
/// Entry i applies once the owner has reached i + 1 phases; past the last entry the last one holds.
/// Null or empty keeps the base interval and copy count throughout.
/// </param>
public sealed class SplitIntoCopies(
    float secondsBetweenSplits,
    int copiesPerSplit = 2,
    float spawnDistance = 60f,
    IReadOnlyList<SplitStep>? phaseSchedule = null)
    : IEnemyAbility
{
    private float _sinceSplit;

    public float SecondsBetweenSplits => secondsBetweenSplits;
    public int CopiesPerSplit => copiesPerSplit;
    public float SpawnDistance => spawnDistance;
    public IReadOnlyList<SplitStep> PhaseSchedule { get; } = phaseSchedule ?? [];

    /// <summary>The interval and copy count in force for an owner that has reached this many phases.</summary>
    public SplitStep StepFor(int phasesReached)
    {
        if (phasesReached <= 0 || PhaseSchedule.Count == 0)
            return new SplitStep(secondsBetweenSplits, copiesPerSplit);
        return PhaseSchedule[Math.Min(phasesReached, PhaseSchedule.Count) - 1];
    }

    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
    {
        if (owner.IsCopy)
            return [];

        var step = StepFor(owner.PhasesReached);

        // The wait is kept across a phase change: an overdue split fires now (only one per update),
        // and the overdue remainder counts toward the next one.
        _sinceSplit += elapsedSeconds;
        if (_sinceSplit < step.SecondsBetweenSplits)
            return [];

        _sinceSplit -= step.SecondsBetweenSplits;

        var copies = new List<Enemy>(step.CopiesPerSplit);
        for (var i = 0; i < step.CopiesPerSplit; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spawnDistance;
            var copy = new Enemy(
                owner.Type,
                owner.Position + offset,
                movement: owner.Type.CreateMovement(owner.Type.Speed),
                experience: 0,
                isCopy: true);
            copies.Add(copy);
        }

        return copies;
    }
}
