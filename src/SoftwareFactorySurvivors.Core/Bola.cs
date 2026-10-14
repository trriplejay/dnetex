using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Two balls joined by a laser-wide line, spinning at a fixed rate around its centre while the centre flies
/// straight at laser speed. It starts across its flight path; every dimension scales with the projectile size upgrade.
/// </summary>
public sealed class Bola : Projectile
{
    /// <summary>Spin rate in radians per second: one full turn every half second.</summary>
    public const float RotationRate = 4f * MathF.PI;

    /// <summary>Ball radius at 100% projectile size, a little more than the line's half-width.</summary>
    public const float BaseBallRadius = 3f;

    public Bola(Vector2 position, float heading, float scale = 1f, int pierce = 0)
        : base(position, heading, 3 * Ship.Speed, scale, pierce)
    {
        // Line direction (cos, sin) = (cos heading, sin heading) is perpendicular to the flight direction.
        Angle = Normalize(heading);
    }

    /// <summary>Direction of the line from ball A to ball B, in radians in [0, 2π).</summary>
    public float Angle { get; private set; }

    /// <summary>Distance between the two ball centres.</summary>
    public float Span => Laser.BaseLength * Scale;
    public float LineWidth => Laser.BaseWidth * Scale;
    public float BallRadius => BaseBallRadius * Scale;

    public Vector2 BallA => Position - Direction * (Span / 2f);
    public Vector2 BallB => Position + Direction * (Span / 2f);

    private Vector2 Direction => new(MathF.Cos(Angle), MathF.Sin(Angle));

    protected override void Advance(float elapsedSeconds) => Angle = Normalize(Angle + RotationRate * elapsedSeconds);

    public override ProjectileShape GetShape()
    {
        var (a, b) = (BallA, BallB);
        return new([new ThickLine(a, b, LineWidth / 2f)], [], [new Circle(a, BallRadius), new Circle(b, BallRadius)]);
    }

    private static float Normalize(float angle)
    {
        var turn = 2f * MathF.PI;
        var wrapped = angle % turn;
        return wrapped < 0f ? wrapped + turn : wrapped;
    }
}
