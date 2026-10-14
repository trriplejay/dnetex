using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// How an enemy moves. Each enemy owns its own instance, so a pattern may keep state (phase, timers).
/// </summary>
public interface IMovementPattern
{
    /// <summary>Returns the enemy's new position after elapsedSeconds.</summary>
    Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds);
}
