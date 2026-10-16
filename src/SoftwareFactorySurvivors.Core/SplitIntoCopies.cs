using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Boss ability that periodically spawns look-alike copy enemies that share the owner's appearance,
/// radius, and movement, but give 0 experience, die in one hit, and never flicker.
/// </summary>
/// <param name="secondsBetweenSplits">Interval between split attempts.</param>
/// <param name="copiesPerSplit">Number of copies to spawn each split.</param>
/// <param name="spawnDistance">Distance offset from the owner's position for copies.</param>
public sealed class SplitIntoCopies(
    float secondsBetweenSplits,
    int copiesPerSplit = 2,
    float spawnDistance = 60f)
    : IEnemyAbility
{
    private float _sinceSplit;

    public float SecondsBetweenSplits => secondsBetweenSplits;
    public int CopiesPerSplit => copiesPerSplit;
    public float SpawnDistance => spawnDistance;

    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
    {
        if (owner.IsCopy)
            return [];

        _sinceSplit += elapsedSeconds;
        if (_sinceSplit < secondsBetweenSplits)
            return [];

        _sinceSplit -= secondsBetweenSplits;

        var copies = new List<Enemy>(copiesPerSplit);
        for (var i = 0; i < copiesPerSplit; i++)
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
