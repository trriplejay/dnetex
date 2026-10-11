using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// The player's ship. Pure game logic with no MonoGame dependency, so it can be unit tested.
/// </summary>
public class Ship
{
    /// <summary>Movement speed in pixels per second.</summary>
    public const float Speed = 200f;

    // Two touching loops and a separate bar, in fixed-up local coordinates.
    // The bar's head (-24) and tail (+24) stick out past the loops by the same amount; the ship faces the mouse, so the front needs no other marker.
    private static readonly Vector2[][] LocalStrokes =
    [
        CreateLoop(-12f),
        CreateLoop(12f),
        [new(0, -24), new(0, 24)],
    ];

    private static Vector2[] CreateLoop(float centerX)
    {
        const int segments = 48;
        var points = new Vector2[segments + 1];
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            points[i] = new Vector2(centerX + 12f * MathF.Cos(angle), 9f * MathF.Sin(angle));
        }
        points[segments] = points[0];
        return points;
    }

    public Ship(Vector2 position)
    {
        Position = position;
    }

    public Vector2 Position { get; private set; }

    /// <summary>Radians clockwise from up (screen -Y); zero faces up.</summary>
    public float Heading { get; private set; } = 0f;

    public Vector2 Forward => new(MathF.Sin(Heading), -MathF.Cos(Heading));

    public Vector2 Nose => Vector2.Transform(new Vector2(0, -24), Matrix3x2.CreateRotation(Heading)) + Position;

    /// <summary>
    /// Moves along a screen direction (up is -Y) regardless of Heading. Directions longer than 1 are
    /// shortened to 1, so diagonals are no faster than straight moves.
    /// </summary>
    public void Move(Vector2 direction, float elapsedSeconds)
    {
        if (direction.LengthSquared() > 1f)
            direction = Vector2.Normalize(direction);
        Position += direction * Speed * elapsedSeconds;
    }

    /// <summary>Turns instantly to face target. Aiming at the ship's own position keeps the current heading.</summary>
    public void AimAt(Vector2 target)
    {
        var toTarget = target - Position;
        if (toTarget.LengthSquared() > 0f)
            Heading = MathF.Atan2(toTarget.X, -toTarget.Y);
    }

    /// <summary>Moves first, then aims from the new position so the nose points exactly at aimTarget.</summary>
    /// <param name="aimTarget">Where to face; null keeps the current heading.</param>
    public void Update(Vector2 direction, Vector2? aimTarget, float elapsedSeconds)
    {
        Move(direction, elapsedSeconds);
        if (aimTarget is { } target)
            AimAt(target);
    }

    /// <summary>The logo's separate strokes rotated by Heading around Position.</summary>
    public IReadOnlyList<Vector2[]> GetStrokes()
    {
        var rotation = Matrix3x2.CreateRotation(Heading);
        return LocalStrokes.Select(stroke => stroke
            .Select(p => Vector2.Transform(p, rotation) + Position).ToArray()).ToArray();
    }
}
