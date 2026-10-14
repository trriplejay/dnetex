using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Circles another enemy at a fixed distance and speed, following it wherever it goes. Ignores the player.
/// </summary>
/// <param name="speed">Speed along the circle in pixels per second; positive is clockwise on screen.</param>
public sealed class Orbit(Enemy center, float radius, float speed, float startAngle) : IMovementPattern
{
    public Enemy Center { get; } = center;
    public float Radius { get; } = radius;
    public float Angle { get; private set; } = startAngle;

    /// <summary>Where on the circle the orbiter currently sits.</summary>
    public Vector2 CurrentPosition => Center.Position + new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * Radius;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds)
    {
        Angle += speed / Radius * elapsedSeconds;
        return CurrentPosition;
    }
}
