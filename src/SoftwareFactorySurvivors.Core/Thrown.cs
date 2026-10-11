using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Flies straight to a landing spot at a fixed speed, ignoring the player, then hands over to the enemy's
/// normal movement pattern for the rest of its life.
/// </summary>
public sealed class Thrown(Vector2 landingSpot, float flightSpeed, IMovementPattern afterLanding) : IMovementPattern
{
    public Vector2 LandingSpot { get; } = landingSpot;
    public bool HasLanded { get; private set; }

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds)
    {
        if (HasLanded)
            return afterLanding.Move(position, playerPosition, elapsedSeconds);

        var toSpot = LandingSpot - position;
        var distance = toSpot.Length();
        var step = flightSpeed * elapsedSeconds;
        if (distance > step)
            return position + toSpot / distance * step;

        HasLanded = true;
        return LandingSpot;
    }
}
