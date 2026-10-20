using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>How often a split happens and how many copies it makes.</summary>
/// <param name="SecondsBetweenSplits">Interval between splits.</param>
/// <param name="CopiesPerSplit">Number of copies each split makes.</param>
public sealed record SplitStep(float SecondsBetweenSplits, int CopiesPerSplit);

/// <summary>
/// Boss ability that periodically spawns look-alike copy enemies that share the owner's appearance and radius
/// and move the way the owner moves now (its current movement recipe, so a phase's pattern and speed once the
/// owner has entered a phase that sets one), but give 0 experience, die in one hit, and never flicker. Each copy
/// gets its own freshly built movement, never the owner's or another copy's instance.
/// The schedule can tighten as the owner reaches its phases: the same instance reads the owner's
/// PhasesReached on every update, so the time since the last split carries over a phase change
/// instead of starting again from zero.
/// A split never takes the live copies past MaxLiveCopies (the game passes how many are alive); a split that
/// comes due at the cap still uses up its interval. No copy spawns closer than MinShipClearance to the ship:
/// a copy whose random spot would be too close is moved out on the side of the owner facing away from the ship.
/// </summary>
/// <param name="secondsBetweenSplits">Interval between split attempts before the owner reaches any phase.</param>
/// <param name="copiesPerSplit">Number of copies to spawn each split before the owner reaches any phase.</param>
/// <param name="spawnDistance">Distance offset from the owner's position for copies.</param>
/// <param name="phaseSchedule">
/// Entry i applies once the owner has reached i + 1 phases; past the last entry the last one holds.
/// Null or empty keeps the base interval and copy count throughout.
/// </param>
/// <param name="maxLiveCopies">Most copies of the owner's type that may be alive at once.</param>
public sealed class SplitIntoCopies(
    float secondsBetweenSplits,
    int copiesPerSplit = 2,
    float spawnDistance = 60f,
    IReadOnlyList<SplitStep>? phaseSchedule = null,
    int maxLiveCopies = 8)
    : IEnemyAbility
{
    /// <summary>No copy spawns closer than this to the ship.</summary>
    public const float MinShipClearance = 100f;

    private float _sinceSplit;

    public float SecondsBetweenSplits => secondsBetweenSplits;
    public int CopiesPerSplit => copiesPerSplit;
    public float SpawnDistance => spawnDistance;
    public int MaxLiveCopies => maxLiveCopies;
    public IReadOnlyList<SplitStep> PhaseSchedule { get; } = phaseSchedule ?? [];

    /// <summary>The interval and copy count in force for an owner that has reached this many phases.</summary>
    public SplitStep StepFor(int phasesReached)
    {
        if (phasesReached <= 0 || PhaseSchedule.Count == 0)
            return new SplitStep(secondsBetweenSplits, copiesPerSplit);
        return PhaseSchedule[Math.Min(phasesReached, PhaseSchedule.Count) - 1];
    }

    /// <summary>Advances the split as if no copies were alive.</summary>
    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random) =>
        Update(owner, playerPosition, elapsedSeconds, random, liveCopies: 0);

    /// <summary>Advances the split; a due split makes only as many copies as fit under MaxLiveCopies.</summary>
    /// <param name="liveCopies">Copies of the owner's type alive now, from every phase.</param>
    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random, int liveCopies)
    {
        if (owner.IsCopy)
            return [];

        var step = StepFor(owner.PhasesReached);

        // The wait is kept across a phase change: an overdue split fires now (only one per update),
        // and the overdue remainder counts toward the next one.
        _sinceSplit += elapsedSeconds;
        if (_sinceSplit < step.SecondsBetweenSplits)
            return [];

        // A split due at the cap still uses up its interval: missed copies are not saved up.
        _sinceSplit -= step.SecondsBetweenSplits;

        var count = Math.Min(step.CopiesPerSplit, Math.Max(0, maxLiveCopies - liveCopies));
        var copies = new List<Enemy>(count);
        for (var i = 0; i < count; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spawnDistance;
            var copy = new Enemy(
                owner.Type,
                SpawnSpot(owner.Position, offset, playerPosition),
                movement: owner.CreateCurrentMovement(),
                experience: 0,
                isCopy: true);
            copies.Add(copy);
        }

        return copies;
    }

    // The boss position plus the random offset, unless that is within MinShipClearance of the ship: then the
    // offset is turned to the far side of the boss from the ship and pushed out along the ship-to-boss
    // direction until the spot clears the ship.
    private static Vector2 SpawnSpot(Vector2 boss, Vector2 offset, Vector2 ship)
    {
        var spot = boss + offset;
        if (Vector2.Distance(spot, ship) >= MinShipClearance)
            return spot;

        var away = boss - ship;
        var direction = away.LengthSquared() > 0f
            ? Vector2.Normalize(away)
            : offset.LengthSquared() > 0f ? Vector2.Normalize(offset) : Vector2.UnitX;

        // Mirror the offset onto the far side of the boss, keeping its sideways part.
        var along = Vector2.Dot(offset, direction);
        if (along < 0f)
            offset -= 2f * along * direction;

        // Push out along the direction by t >= 0 so that |v + t * direction| reaches the clearance, where v is the
        // spot relative to the ship. A hair over the clearance absorbs float rounding.
        var target = MinShipClearance + 0.01f;
        var v = boss + offset - ship;
        var vAlong = Vector2.Dot(v, direction);
        var t = -vAlong + MathF.Sqrt(MathF.Max(0f, vAlong * vAlong - v.LengthSquared() + target * target));
        return boss + offset + MathF.Max(0f, t) * direction;
    }
}
