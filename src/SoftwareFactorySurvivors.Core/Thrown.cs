using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Flies straight to a landing spot at a fixed speed, ignoring the player, then hands over to the enemy's
/// normal movement pattern for the rest of its life.
/// </summary>
public sealed class Thrown(Vector2 landingSpot, float flightSpeed, IMovementPattern afterLanding, bool harmlessUntilLanded = false) : IMovementPattern
{
    public Vector2 LandingSpot { get; } = landingSpot;
    public bool HasLanded { get; private set; }
    public bool IsHarmlessInFlight => harmlessUntilLanded && !HasLanded;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) =>
        Move(position, playerPosition, elapsedSeconds, 1f);

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor)
    {
        if (HasLanded)
            return afterLanding.Move(position, playerPosition, elapsedSeconds, speedFactor);

        var toSpot = LandingSpot - position;
        var distance = toSpot.Length();
        var step = flightSpeed * elapsedSeconds;
        var slowedStep = step * speedFactor;
        if (distance <= slowedStep)
        {
            HasLanded = true;
            return LandingSpot;
        }

        if (distance > step)
            return Displacement.Scale(position, position + toSpot / distance * step, speedFactor);

        // A slowed flight covers only part of the last stretch; land when on the spot or within the step.
        var next = Displacement.Scale(position, LandingSpot, speedFactor);
        HasLanded = next == LandingSpot;
        return next;
    }
}
