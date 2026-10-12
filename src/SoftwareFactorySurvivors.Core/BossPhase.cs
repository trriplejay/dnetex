namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// A change in an enemy's behaviour once its hit points fall to a fraction of its maximum, e.g. a boss
/// getting more aggressive at 75%, 50% and 25%. Whatever a phase sets replaces the enemy's current
/// ability or movement; whatever it leaves null carries over from before.
/// </summary>
/// <param name="AtHealthFraction">Takes effect when hit points drop to this fraction of the maximum or below (0–1).</param>
/// <param name="CreateAbility">The ability from this phase on; null keeps the current one.</param>
/// <param name="CreateMovement">The movement pattern from this phase on; null keeps the current one.</param>
public sealed record BossPhase(
    float AtHealthFraction,
    Func<IEnemyAbility>? CreateAbility = null,
    Func<IMovementPattern>? CreateMovement = null);
