using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Turns the held WASD keys into a screen movement direction.</summary>
public static class ShipInput
{
    /// <summary>
    /// W up, A left, S down, D right. Opposite keys cancel, and diagonals are normalized so they move at
    /// the same speed as a single key.
    /// </summary>
    public static Vector2 Direction(bool up, bool left, bool down, bool right)
    {
        var direction = new Vector2((right ? 1 : 0) - (left ? 1 : 0), (down ? 1 : 0) - (up ? 1 : 0));
        return direction == Vector2.Zero ? direction : Vector2.Normalize(direction);
    }
}
