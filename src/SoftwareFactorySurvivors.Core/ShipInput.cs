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

/// <summary>Converts movement keys to independent steering and thrust inputs.</summary>
public static class ShipInput
{
    public static float KeyToTurn(MovementKey key) => key switch
    {
        MovementKey.A => -1f,
        MovementKey.D => 1f,
        _ => 0f,
    };

    public static float KeyToThrust(MovementKey key) => key switch
    {
        MovementKey.W => 1f,
        MovementKey.S => -1f,
        _ => 0f,
    };
}
