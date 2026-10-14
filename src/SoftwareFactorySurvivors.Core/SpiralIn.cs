using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Circles the player while slowly closing in: it always flies at a fixed angle off the direct line to the player,
/// so a larger angle gives a wider, slower spiral.
/// </summary>
/// <param name="offsetDegrees">Angle between its heading and the direct line to the player (0 chases, 90 orbits).</param>
public sealed class SpiralIn(float speed, float offsetDegrees) : IMovementPattern
{
    private readonly float _offset = offsetDegrees * MathF.PI / 180f;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds)
    {
        var toPlayer = playerPosition - position;
        var distance = toPlayer.Length();
        var step = speed * elapsedSeconds;
        if (distance <= step)
            return playerPosition;

        var heading = Vector2.Transform(toPlayer / distance, Matrix3x2.CreateRotation(_offset));
        return position + heading * step;
    }
}
