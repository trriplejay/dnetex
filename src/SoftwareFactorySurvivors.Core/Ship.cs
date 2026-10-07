using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// The player's ship. Pure game logic with no MonoGame dependency, so it can be unit tested.
/// </summary>
public class Ship
{
    /// <summary>Movement speed in pixels per second.</summary>
    public const float Speed = 200f;

    // Triangle outline relative to the ship's center, pointing up (screen Y grows downward).
    private static readonly Vector2[] Shape =
    [
        new(0, -12),
        new(9, 9),
        new(-9, 9),
    ];

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

    /// <summary>The triangle's corners in world (screen) coordinates.</summary>
    public Vector2[] GetVertices() => Shape.Select(p => p + Position).ToArray();
}
