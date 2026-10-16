using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Alternates a telegraph phase and a charge phase.
/// During telegraph (at least 1.0 second), locks a straight segment from its position to the player position
/// and exposes it publicly as a readable aim line without displacing along it.
/// During charge, moves along that locked segment at high speed until reaching the end or max distance.
/// </summary>
public sealed class ChargeAlongLine(
    float telegraphSeconds = 1.2f,
    float chargeSpeed = 450f,
    float chargeDistance = 600f) : IMovementPattern
{
    public float TelegraphSeconds { get; } = MathF.Max(1.0f, telegraphSeconds);
    public float ChargeSpeed { get; } = chargeSpeed;
    public float ChargeDistance { get; } = chargeDistance;

    private float _timeInPhase;
    private bool _isCharging;
    private bool _hasLockedFirstSegment;

    public bool IsTelegraphing => !_isCharging;
    public bool IsCharging => _isCharging;

    public Vector2 AimStart { get; private set; }
    public Vector2 AimEnd { get; private set; }

    /// <summary>The full locked aim segment (AimStart to AimEnd).</summary>
    public (Vector2 Start, Vector2 End) LockedSegment => (AimStart, AimEnd);

    /// <summary>Segment traversed during the last charge move or phase, useful for laying hazards.</summary>
    public (Vector2 Start, Vector2 End) LastChargeSegment { get; private set; }

    private float _chargedDistance;
    private Vector2 _chargeDirection;
    private float _totalSegmentLength;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) =>
        Move(position, playerPosition, elapsedSeconds, 1f);

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor)
    {
        if (elapsedSeconds <= 0f)
            return position;

        if (!_isCharging)
        {
            // If just starting, lock the initial aim segment
            if (!_hasLockedFirstSegment)
            {
                _hasLockedFirstSegment = true;
                LockSegment(position, playerPosition);
            }

            _timeInPhase += elapsedSeconds;
            if (_timeInPhase >= TelegraphSeconds)
            {
                _isCharging = true;
                _timeInPhase = 0f;
                _chargedDistance = 0f;
            }

            // During telegraph, position does not move along the line
            return position;
        }

        // We are charging along the locked segment
        var maxDistance = MathF.Min(ChargeDistance, _totalSegmentLength);
        var remainingDistance = MathF.Max(0f, maxDistance - _chargedDistance);

        var moveStep = ChargeSpeed * elapsedSeconds * speedFactor;
        var actualStep = MathF.Min(moveStep, remainingDistance);

        var nextPosition = position + _chargeDirection * actualStep;
        LastChargeSegment = (position, nextPosition);
        _chargedDistance += actualStep;
        _timeInPhase += elapsedSeconds;

        if (_chargedDistance >= maxDistance || (_chargeDirection == Vector2.Zero))
        {
            // Charge complete, reset to telegraph for next cycle
            _isCharging = false;
            _timeInPhase = 0f;
            LockSegment(nextPosition, playerPosition);
        }

        return nextPosition;
    }

    private void LockSegment(Vector2 start, Vector2 target)
    {
        AimStart = start;
        var diff = target - start;
        var dist = diff.Length();
        if (dist > 0f)
        {
            _chargeDirection = diff / dist;
            _totalSegmentLength = MathF.Min(ChargeDistance, dist);
            AimEnd = start + _chargeDirection * _totalSegmentLength;
        }
        else
        {
            _chargeDirection = Vector2.UnitX;
            _totalSegmentLength = 0f;
            AimEnd = start;
        }
    }
}
