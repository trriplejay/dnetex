using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// The player's ship. Pure game logic with no MonoGame dependency, so it can be unit tested.
/// </summary>
public class Ship
{
    /// <summary>Movement speed in pixels per second.</summary>
    public const float Speed = 200f;

    /// <summary>Angular speed in radians per second (one full turn).</summary>
    public const float RotationSpeed = 2f * MathF.PI;

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

    /// <summary>Orientation in radians: zero points up; positive turns right on screen.</summary>
    public float Heading { get; private set; }

    /// <summary>
    /// Applies rotation first, then thrust along the resulting heading for this frame.
    /// Thrust is +1 forward / -1 backward; rotation is -1 left / +1 right; zero is idle.
    /// </summary>
    public void Update(float thrust, float rotation, float elapsedSeconds)
    {
        Heading += rotation * RotationSpeed * elapsedSeconds;

        var forward = Vector2.Transform(-Vector2.UnitY, Matrix3x2.CreateRotation(Heading));
        Position += forward * thrust * Speed * elapsedSeconds;
    }

    /// <summary>The triangle's corners rotated about Position in world (screen) coordinates.</summary>
    public Vector2[] GetVertices()
    {
        var rotation = Matrix3x2.CreateRotation(Heading);
        return Shape.Select(p => Vector2.Transform(p, rotation) + Position).ToArray();
    }
}
