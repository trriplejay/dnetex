using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

public sealed class Laser : Projectile
{
    /// <summary>Length at 100% projectile size: half the ship's original 26-pixel nose-to-tail extent.</summary>
    public const float BaseLength = 13f;

    /// <summary>Drawn and hit-tested width at 100% projectile size.</summary>
    public const float BaseWidth = 4f;

    private readonly HashSet<Enemy> _hitEnemies = [];

    /// <param name="scale">Multiplies width and length together (the projectile size upgrade).</param>
    /// <param name="pierce">How many extra enemies it passes through before it's used up.</param>
    public Laser(Vector2 position, float heading, float scale = 1f, int pierce = 0)
        : base(position, heading, 3 * Ship.Speed)
    {
        Scale = scale;
        PierceRemaining = pierce;
    }

    public float Scale { get; }
    public float Length => BaseLength * Scale;
    public float Width => BaseWidth * Scale;
    public int PierceRemaining { get; private set; }

    public override Vector2[] GetLine() => [Position, Position + Forward * Length];

    /// <summary>A piercing laser hits each enemy at most once.</summary>
    public bool HasHit(Enemy enemy) => _hitEnemies.Contains(enemy);

    /// <summary>Records a hit and returns true when the laser is used up and should disappear.</summary>
    public bool RegisterHit(Enemy enemy)
    {
        _hitEnemies.Add(enemy);
        if (PierceRemaining == 0)
            return true;

        PierceRemaining--;
        return false;
    }
}
