using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Creeps toward the player and every few seconds teleports a fixed distance closer.</summary>
public sealed class Blink(float creepSpeed, float jumpDistance, float secondsBetweenJumps) : IMovementPattern
{
    private float _sinceJump;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds)
    {
        position = new ChasePlayer(creepSpeed).Move(position, playerPosition, elapsedSeconds);

        _sinceJump += elapsedSeconds;
        if (_sinceJump < secondsBetweenJumps)
            return position;

        _sinceJump -= secondsBetweenJumps;
        var toPlayer = playerPosition - position;
        var distance = toPlayer.Length();
        return distance <= jumpDistance ? playerPosition : position + toPlayer / distance * jumpDistance;
    }
}
