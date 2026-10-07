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
    // Screen Y grows downward: the front (-10) is shorter than the rear (+16).
    private static readonly Vector2[][] LocalStrokes =
    [
        CreateLoop(-8f),
        CreateLoop(8f),
        [new(0, -10), new(0, 16)],
    ];

    private static Vector2[] CreateLoop(float centerX)
    {
        const int segments = 48;
        var points = new Vector2[segments + 1];
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            points[i] = new Vector2(centerX + 8f * MathF.Cos(angle), 6f * MathF.Sin(angle));
        }
        points[segments] = points[0];
        return points;
    }

    public Ship(Vector2 position)
    {
        Position = position;
    }

    public Vector2 Position { get; private set; }

    /// <summary>
    /// Moves the ship in <paramref name="direction"/> for <paramref name="elapsedSeconds"/>.
    /// Diagonal input is normalized so it isn't faster than straight movement.
    /// </summary>
    public void Update(Vector2 direction, float elapsedSeconds)
    {
        if (direction == Vector2.Zero)
            return;

        Position += Vector2.Normalize(direction) * Speed * elapsedSeconds;
    }

    /// <summary>The logo's separate strokes in world (screen) coordinates, without rotation.</summary>
    public IReadOnlyList<Vector2[]> GetStrokes() =>
        LocalStrokes.Select(stroke => stroke.Select(p => p + Position).ToArray()).ToArray();
}
