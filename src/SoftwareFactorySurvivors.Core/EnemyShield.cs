namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Enemies that orbit their owner and protect it: the owner can't be damaged while any of them is alive.
/// They're spaced evenly around a circle of OrbitRadius and fly at SpeedMultiplier times their type's speed.
/// </summary>
public sealed record EnemyShield(EnemyType Type, int Count, float OrbitRadius, float SpeedMultiplier);
