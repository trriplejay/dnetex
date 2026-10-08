using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>A straight-flying projectile whose drawable segment determines its expiry.</summary>
public abstract class Projectile
{
    protected Projectile(Vector2 position, float heading, float speed)
    {
        Position = position;
        Heading = heading;
        Speed = speed;
    }

    public Vector2 Position { get; private set; }
    public float Heading { get; }
    public float Speed { get; }

    protected Vector2 Forward => new(MathF.Sin(Heading), -MathF.Cos(Heading));

    public void Update(float elapsedSeconds)
    {
        Position += Forward * Speed * elapsedSeconds;
    }

    public abstract Vector2[] GetLine();

    /// <summary>True only when the entire segment is outside [0,width] × [0,height].</summary>
    public bool IsExpired(float width, float height)
    {
        var line = GetLine();
        var direction = line[1] - line[0];
        var enter = 0f;
        var exit = 1f;
        return !IntersectsAxis(line[0].X, direction.X, width, ref enter, ref exit)
            || !IntersectsAxis(line[0].Y, direction.Y, height, ref enter, ref exit);
    }

    // Clip the segment's parameter interval against both closed rectangle slabs.
    private static bool IntersectsAxis(float origin, float direction, float maximum, ref float enter, ref float exit)
    {
        if (direction == 0f)
            return origin >= 0f && origin <= maximum;

        var first = -origin / direction;
        var last = (maximum - origin) / direction;
        enter = MathF.Max(enter, MathF.Min(first, last));
        exit = MathF.Min(exit, MathF.Max(first, last));
        return enter <= exit;
    }
}
