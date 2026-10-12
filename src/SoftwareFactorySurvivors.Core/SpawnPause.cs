namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// A lull: regular edge spawning stops while the survival clock is from FromSeconds up to ToSeconds, then
/// carries on at its normal rate. The spawner's timing is frozen meanwhile, so no spawns are saved up.
/// </summary>
public sealed record SpawnPause(float FromSeconds, float ToSeconds)
{
    public bool Covers(float seconds) => seconds >= FromSeconds && seconds < ToSeconds;
}
