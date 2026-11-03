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

    public Vector2[] GetLine() => [Position, Position + Forward * Length];

    /// <summary>A single thick segment.</summary>
    protected override ProjectileShape BuildShape() =>
        new([new ThickLine(Position, Position + Forward * Length, Width / 2f)], [], []);
}
