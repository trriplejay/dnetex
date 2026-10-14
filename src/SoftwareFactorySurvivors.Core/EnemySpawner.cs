using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Spawns each registered enemy type just off a random screen edge at that type's own rate.
/// Rates can be changed at any time; a change applies from the next Update, without resetting progress.
/// </summary>
public sealed class EnemySpawner(Random random)
{
    private sealed class Entry(EnemyType type, float spawnsPerMinute)
    {
        public EnemyType Type { get; } = type;
        public float SpawnsPerMinute { get; set; } = spawnsPerMinute;

        /// <summary>Fraction of the way to the next spawn; one spawn per whole unit.</summary>
        public double Progress { get; set; }
    }

    private readonly List<Entry> _entries = [];

    public IEnumerable<EnemyType> Types => _entries.Select(entry => entry.Type);

    /// <summary>Starts spawning type at its DefaultSpawnsPerMinute; adding a type twice changes nothing.</summary>
    public void Add(EnemyType type)
    {
        if (Find(type) is null)
            _entries.Add(new Entry(type, ValidRate(type.DefaultSpawnsPerMinute)));
    }

    public float GetSpawnsPerMinute(EnemyType type) =>
        (Find(type) ?? throw UnknownType(type)).SpawnsPerMinute;

    /// <summary>Changes how often type spawns. Zero pauses it; the type must already be added.</summary>
    public void SetSpawnsPerMinute(EnemyType type, float spawnsPerMinute)
    {
        var entry = Find(type) ?? throw UnknownType(type);
        entry.SpawnsPerMinute = ValidRate(spawnsPerMinute);
    }

    public IReadOnlyList<Enemy> Update(float elapsedSeconds, float width, float height)
    {
        var enemies = new List<Enemy>();
        foreach (var entry in _entries)
        {
            entry.Progress += entry.SpawnsPerMinute / 60d * elapsedSeconds;
            while (entry.Progress >= 1d)
            {
                enemies.Add(entry.Type.Create(EdgePoint(width, height, entry.Type.Radius)));
                entry.Progress -= 1d;
            }
        }
        return enemies;
    }

    /// <summary>Creates one enemy of type just off a random screen edge, outside the regular schedule.</summary>
    public Enemy SpawnAtEdge(EnemyType type, float width, float height) =>
        type.Create(EdgePoint(width, height, type.Radius));

    private Entry? Find(EnemyType type) => _entries.FirstOrDefault(entry => ReferenceEquals(entry.Type, type));

    private static ArgumentException UnknownType(EnemyType type) =>
        new($"Enemy type '{type.Name}' has not been added to the spawner.", nameof(type));

    private static float ValidRate(float spawnsPerMinute) =>
        float.IsFinite(spawnsPerMinute) && spawnsPerMinute >= 0f
            ? spawnsPerMinute
            : throw new ArgumentOutOfRangeException(nameof(spawnsPerMinute));

    private Vector2 EdgePoint(float width, float height, float margin)
    {
        var along = random.NextSingle();
        return random.Next(4) switch
        {
            0 => new Vector2(along * width, -margin),
            1 => new Vector2(width + margin, along * height),
            2 => new Vector2(along * width, height + margin),
            _ => new Vector2(-margin, along * height),
        };
    }
}
