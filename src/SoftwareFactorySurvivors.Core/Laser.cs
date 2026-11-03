using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

public sealed class Laser : Projectile
{
    /// <summary>Length at 100% projectile size: half the ship's original 26-pixel nose-to-tail extent.</summary>
    public const float BaseLength = 13f;

    /// <summary>Drawn and hit-tested width at 100% projectile size.</summary>
    public const float BaseWidth = 4f;

    /// <param name="scale">Multiplies width and length together (the projectile size upgrade).</param>
    /// <param name="pierce">How many extra enemies it passes through before it's used up.</param>
    /// <param name="damage">Damage dealt per hit by this laser shot.</param>
    public Laser(Vector2 position, float heading, float scale = 1f, int pierce = 0, float damage = 1f)
        : base(position, heading, 3 * Ship.Speed, scale, pierce, damage)
    {
    }

    public float Length => BaseLength * Scale;
    public float Width => BaseWidth * Scale;

    /// <summary>Radius of the level-5 impact explosion in pixels.</summary>
    public const float ExplosionRadius = 40f;

    /// <summary>The laser level from which an impact explodes.</summary>
    public const int ExplosionLevel = 5;

    /// <summary>
    /// Identifies all other live enemies within the level-5 explosion radius (40 px) of an impact point.
    /// Below level 5, returns an empty list.
    /// </summary>
    /// <param name=\"impactPoint\">Position where the laser struck.</param>
    /// <param name=\"enemies\">Pool of candidate enemies.</param>
    /// <param name=\"weaponLevel\">Current level of the laser weapon.</param>
    /// <param name=\"struckEnemy\">The primary enemy directly struck by the laser, if any, to exclude from splash damage.</param>
    public static IReadOnlyList<Enemy> GetExplosionTargets(
        Vector2 impactPoint,
        IEnumerable<Enemy> enemies,
        int weaponLevel,
        Enemy? struckEnemy = null)
    {
        if (weaponLevel < ExplosionLevel)
            return Array.Empty<Enemy>();

        var targets = new List<Enemy>();
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead)
                continue;
            if (struckEnemy != null && ReferenceEquals(enemy, struckEnemy))
                continue;

            if (Vector2.Distance(impactPoint, enemy.Position) <= ExplosionRadius)
                targets.Add(enemy);
        }
        return targets;
    }

    /// <summary>
    /// Applies the level-5 explosion to all other live enemies within 40 px of an impact point, dealing the shot damage.
    /// Below level 5, deals no explosion damage. Returns the damaged enemies.
    /// </summary>
    public static IReadOnlyList<Enemy> ApplyExplosion(
        Vector2 impactPoint,
        float damage,
        int weaponLevel,
        IEnumerable<Enemy> enemies,
        Enemy? struckEnemy = null)
    {
        var targets = GetExplosionTargets(impactPoint, enemies, weaponLevel, struckEnemy);
        foreach (var enemy in targets)
        {
            enemy.TakeDamage(damage);
        }
        return targets;
    }

    public Vector2[] GetLine() => [Position, Position + Forward * Length];

    /// <summary>A single thick segment.</summary>
    protected override ProjectileShape BuildShape() =>
        new([new ThickLine(Position, Position + Forward * Length, Width / 2f)], [], []);
}
