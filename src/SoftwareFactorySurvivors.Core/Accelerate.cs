using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Chases the player, starting slow and speeding up the longer it lives, up to a top speed.</summary>
public sealed class Accelerate(float startSpeed, float acceleration, float maxSpeed) : IMovementPattern
{
    public float CurrentSpeed { get; private set; } = startSpeed;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) =>
        Move(position, playerPosition, elapsedSeconds, 1f);

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor)
    {
        // The ramp runs on real time, so a slow only shortens the step and leaves no lag once it ends.
        CurrentSpeed = MathF.Min(CurrentSpeed + acceleration * elapsedSeconds, maxSpeed);
        return new ChasePlayer(CurrentSpeed).Move(position, playerPosition, elapsedSeconds, speedFactor);
    }
}
