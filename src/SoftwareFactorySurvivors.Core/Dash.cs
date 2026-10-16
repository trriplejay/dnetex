using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Creeps toward the player, then lunges: it locks onto where the player is and charges in a straight line
/// at high speed, then creeps again. The lunge doesn't steer, so the player can sidestep it.
/// </summary>
public sealed class Dash(float creepSpeed, float dashSpeed, float creepSeconds, float dashSeconds) : IMovementPattern
{
    private float _phaseTime;
    private bool _dashing;
    private Vector2 _dashDirection;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) =>
        Move(position, playerPosition, elapsedSeconds, 1f);

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor)
    {
        _phaseTime += elapsedSeconds;
        if (!_dashing && _phaseTime >= creepSeconds)
        {
            _dashing = true;
            _phaseTime = 0f;
            var toPlayer = playerPosition - position;
            _dashDirection = toPlayer.LengthSquared() > 0f ? Vector2.Normalize(toPlayer) : Vector2.Zero;
        }
        else if (_dashing && _phaseTime >= dashSeconds)
        {
            _dashing = false;
            _phaseTime = 0f;
        }

        if (_dashing)
            return position + _dashDirection * dashSpeed * elapsedSeconds * speedFactor;

        return new ChasePlayer(creepSpeed).Move(position, playerPosition, elapsedSeconds, speedFactor);
    }
}
