using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// The player's ship. Pure game logic with no MonoGame dependency, so it can be unit tested.
/// </summary>
public class Ship
{
    /// <summary>Movement speed in pixels per second.</summary>
    public const float Speed = 200f;

    /// <summary>Turning speed in radians per second.</summary>
    public const float TurnSpeed = MathF.PI;

    // Two touching loops and a separate bar, in fixed-up local coordinates.
    // Screen Y grows downward: the centre bar is equal-length, 16 forward and 16 back from centre.
    private static readonly Vector2[][] LocalStrokes =
    [
        CreateLoop(-8f),
        CreateLoop(8f),
        [new(0, -16), new(0, 16)],
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

    /// <summary>Radians clockwise from up (screen -Y); zero faces up.</summary>
    public float Heading { get; private set; } = 0f;

    public Vector2 Forward => new(MathF.Sin(Heading), -MathF.Cos(Heading));

    public Vector2 Nose => Vector2.Transform(new Vector2(0, -16), Matrix3x2.CreateRotation(Heading)) + Position;

    /// <summary>Turns in place: negative input turns left, positive input turns right.</summary>
    public void Turn(float turn, float elapsedSeconds)
    {
        Heading += turn * TurnSpeed * elapsedSeconds;
    }

    /// <summary>Thrusts forward (positive) or backward (negative) along Heading.</summary>
    public void Thrust(float thrust, float elapsedSeconds)
    {
        Position += Forward * thrust * Speed * elapsedSeconds;
    }

    /// <summary>Applies steering before thrust for this frame.</summary>
    public void Update(float turn, float thrust, float elapsedSeconds)
    {
        Turn(turn, elapsedSeconds);
        Thrust(thrust, elapsedSeconds);
    }

    /// <summary>The logo's separate strokes rotated by Heading around Position.</summary>
    public IReadOnlyList<Vector2[]> GetStrokes()
    {
        var rotation = Matrix3x2.CreateRotation(Heading);
        return LocalStrokes.Select(stroke => stroke
            .Select(p => Vector2.Transform(p, rotation) + Position).ToArray()).ToArray();
    }
}
