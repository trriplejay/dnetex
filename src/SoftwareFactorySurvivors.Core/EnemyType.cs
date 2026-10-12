using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Everything that makes one kind of enemy: its logo outline (in local coordinates around its centre),
/// how tough and fast it is, how it moves, how much experience it's worth, and when and how often it spawns.
/// </summary>
/// <param name="Radius">Distance from the centre to the outline's furthest point; also how far off screen it spawns.</param>
/// <param name="Speed">Base movement speed in pixels per second, handed to CreateMovement.</param>
/// <param name="Experience">Experience the player gains for killing one.</param>
/// <param name="ArrivesAtSeconds">Survival time at which this type starts spawning.</param>
/// <param name="SpinSpeed">How fast the logo rotates, in radians per second (0 keeps it upright).</param>
/// <param name="CreateMovement">Builds a fresh movement pattern for each enemy, given Speed.</param>
/// <param name="CreateAbility">Builds a fresh special ability for each enemy, e.g. a boss throwing minions; null for none.</param>
/// <param name="Phases">Behaviour changes as the enemy loses health, from the highest AtHealthFraction down; null for none.</param>
public sealed record EnemyType(
    string Name,
    float Radius,
    float HitPoints,
    float Speed,
    int Experience,
    float DefaultSpawnsPerMinute,
    float ArrivesAtSeconds,
    float SpinSpeed,
    Func<float, IMovementPattern> CreateMovement,
    IReadOnlyList<Vector2[]> Outline,
    Func<IEnemyAbility>? CreateAbility = null,
    IReadOnlyList<BossPhase>? Phases = null)
{
    public Enemy Create(Vector2 position) => new(this, position);
}
