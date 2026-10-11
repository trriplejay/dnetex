namespace SoftwareFactorySurvivors.Core;

/// <summary>A boss that enters once, when the survival clock reaches AtSeconds.</summary>
public sealed record BossArrival(float AtSeconds, EnemyType Type);
