using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

public enum MovementKey
{
    None,
    W,
    A,
    S,
    D,
    Up,
    Down,
    Left,
    Right,
}

/// <summary>Converts movement keys to directions in screen coordinates.</summary>
public static class ShipInput
{
    public static Vector2 KeyToDirection(MovementKey key) => key switch
    {
        MovementKey.W => new Vector2(0, -1),
        MovementKey.A => new Vector2(-1, 0),
        MovementKey.S => new Vector2(0, 1),
        MovementKey.D => new Vector2(1, 0),
        _ => Vector2.Zero,
    };
}
