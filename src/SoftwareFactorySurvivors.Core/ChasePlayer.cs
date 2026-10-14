using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Heads straight for the player at a constant speed, stopping on them rather than overshooting.</summary>
public sealed class ChasePlayer(float speed) : IMovementPattern
{
    public float Speed { get; } = speed;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds)
    {
        var toPlayer = playerPosition - position;
        var distance = toPlayer.Length();
        var step = Speed * elapsedSeconds;
        return distance <= step ? playerPosition : position + toPlayer / distance * step;
    }
}
