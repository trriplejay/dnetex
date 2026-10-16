using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Creeps toward the player and every few seconds teleports a fixed distance closer.</summary>
public sealed class Blink(float creepSpeed, float jumpDistance, float secondsBetweenJumps) : IMovementPattern
{
    private float _sinceJump;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) =>
        Move(position, playerPosition, elapsedSeconds, 1f);

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor)
    {
        // The whole unslowed move (creep, then any jump with its snap onto the player) is worked out first and only
        // the result is scaled, so a slowed jump covers a quarter of the real jump, not a quarter-length jump.
        var next = new ChasePlayer(creepSpeed).Move(position, playerPosition, elapsedSeconds);

        _sinceJump += elapsedSeconds;
        if (_sinceJump >= secondsBetweenJumps)
        {
            _sinceJump -= secondsBetweenJumps;
            var toPlayer = playerPosition - next;
            var distance = toPlayer.Length();
            next = distance <= jumpDistance ? playerPosition : next + toPlayer / distance * jumpDistance;
        }

        return Displacement.Scale(position, next, speedFactor);
    }
}
