using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// A straight-flying projectile. Its shape (see GetShape) is what's drawn, what hits enemies and what decides
/// when it has left the screen. It damages each enemy at most once and is used up after 1 + pierce enemies.
/// </summary>
public abstract class Projectile
{
    private readonly HashSet<Enemy> _hitEnemies = [];
    protected ProjectileShape? _shape;

    /// <param name="scale">Multiplies every dimension of the shape (the projectile size upgrade).</param>
    /// <param name="pierce">How many extra enemies it passes through before it's used up.</param>
    protected Projectile(Vector2 position, float heading, float speed, float scale = 1f, int pierce = 0)
    {
        Position = position;
        Heading = heading;
        Speed = speed;
        Scale = scale;
        PierceRemaining = pierce;
    }

    public Vector2 Position { get; private set; }
    public float Heading { get; }
    public float Speed { get; }
    public float Scale { get; }
    public int PierceRemaining { get; private set; }

    protected Vector2 Forward => new(MathF.Sin(Heading), -MathF.Cos(Heading));

    /// <summary>Moves and advances the projectile, then refreshes its shape once for the rest of the frame.</summary>
    public void Update(float elapsedSeconds)
    {
        Position += Forward * Speed * elapsedSeconds;
        Advance(elapsedSeconds);
        if (_shape == null)
            _shape = BuildShape();
        else
            RefreshShape();
    }

    /// <summary>Per-frame state beyond straight flight, e.g. spinning; called after the position moves.</summary>
    protected virtual void Advance(float elapsedSeconds)
    {
    }

    /// <summary>Updates the existing shape in place without allocating new geometry where possible.</summary>
    protected virtual void RefreshShape()
    {
        if (this is Laser laser && _shape != null && _shape.Lines is ThickLine[] lines && lines.Length == 1)
        {
            lines[0] = new ThickLine(Position, Position + Forward * laser.Length, laser.Width / 2f);
            _shape.InvalidateBounds();
            return;
        }

        _shape = BuildShape();
    }

    /// <summary>
    /// The current shape in screen coordinates, used for drawing, hits and expiry alike. It is built once per
    /// frame (after Update moves and advances the projectile) and the same instance is returned until the next
    /// Update; before the first Update it is built on first read.
    /// </summary>
    public ProjectileShape GetShape() => _shape ??= BuildShape();

    /// <summary>Builds the shape from the current position and state; GetShape caches the result.</summary>
    protected abstract ProjectileShape BuildShape();

    /// <summary>True when the projectile's shape touches the given enemy outline.</summary>
    public bool Touches(IReadOnlyList<Vector2[]> enemyStrokes) => GetShape().Touches(enemyStrokes);

    /// <summary>True only when the entire shape is outside [0,width] × [0,height].</summary>
    public bool IsExpired(float width, float height) => !GetShape().Overlaps(width, height);

    /// <summary>A piercing projectile hits each enemy at most once.</summary>
    public bool HasHit(Enemy enemy) => _hitEnemies.Contains(enemy);

    /// <summary>Records a hit and returns true when the projectile is used up and should disappear.</summary>
    public bool RegisterHit(Enemy enemy)
    {
        _hitEnemies.Add(enemy);
        if (PierceRemaining == 0)
            return true;

        PierceRemaining--;
        return false;
    }
}
