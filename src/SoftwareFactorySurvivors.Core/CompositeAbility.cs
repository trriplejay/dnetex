using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Combines multiple abilities into a single composite ability, forwarding update ticks
/// and aggregating any created enemies and produced hazards.
/// </summary>
public sealed class CompositeAbility(params IEnemyAbility[] abilities) : IEnemyAbility, IHazardProducer
{
    private readonly IReadOnlyList<IEnemyAbility> _abilities = [.. abilities];

    public IReadOnlyList<IEnemyAbility> Abilities => _abilities;

    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
    {
        List<Enemy>? created = null;
        foreach (var ability in _abilities)
        {
            var enemies = ability.Update(owner, playerPosition, elapsedSeconds, random);
            if (enemies.Count > 0)
            {
                created ??= [];
                created.AddRange(enemies);
            }
        }
        return created ?? (IReadOnlyList<Enemy>)[];
    }

    public IReadOnlyList<Hazard> TakeHazards()
    {
        List<Hazard>? hazards = null;
        foreach (var ability in _abilities)
        {
            if (ability is IHazardProducer producer)
            {
                var produced = producer.TakeHazards();
                if (produced.Count > 0)
                {
                    hazards ??= [];
                    hazards.AddRange(produced);
                }
            }
        }
        return hazards ?? (IReadOnlyList<Hazard>)[];
    }
}
