using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// How an enemy moves. Each enemy owns its own instance, so a pattern may keep state (phase, timers).
/// </summary>
public interface IMovementPattern
{
    /// <summary>Returns the enemy's new position after elapsedSeconds.</summary>
    Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds);

    /// <summary>
    /// Returns the enemy's new position after elapsedSeconds, covering speedFactor times its normal distance (a
    /// Bola slow passes 0.25). Only the displacement is scaled: timers, ramps and phases still run on the real
    /// elapsed time, so the pattern moves exactly as normal once the factor is back to 1. Patterns that keep such
    /// state override this; the default scales the plain move's displacement.
    /// </summary>
    Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor) =>
        Displacement.Scale(position, Move(position, playerPosition, elapsedSeconds), speedFactor);
}
