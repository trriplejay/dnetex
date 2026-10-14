using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// When the survival clock reaches AtSeconds, Count enemies of Type appear evenly spaced on a circle of
/// Radius centred on the ship, at a random rotation, and close in from all sides.
/// </summary>
public sealed record RingSpawn(float AtSeconds, EnemyType Type, int Count, float Radius)
{
    public IEnumerable<Enemy> Spawn(Vector2 center, Random random)
    {
        var rotation = random.NextSingle() * MathF.Tau;
        for (var i = 0; i < Count; i++)
        {
            var angle = rotation + MathF.Tau * i / Count;
            yield return Type.Create(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Radius);
        }
    }
}
