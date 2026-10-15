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

    private readonly ThickLine[] _lines = new ThickLine[1];
    private readonly Circle[] _circles = new Circle[2];

    public Bola(Vector2 position, float heading, float scale = 1f, int pierce = 0)
        : base(position, heading, 3 * Ship.Speed, scale, pierce)
    {
        // Line direction (cos, sin) = (cos heading, sin heading) is perpendicular to the flight direction.
        Angle = Normalize(heading);
        UpdateGeometry();
        _shape = new ProjectileShape(_lines, [], _circles);
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

    protected override ProjectileShape BuildShape() => _shape!;

    protected override void RefreshShape() => UpdateGeometry();

    private void UpdateGeometry()
    {
        var (a, b) = (BallA, BallB);
        _lines[0] = new ThickLine(a, b, LineWidth / 2f);
        _circles[0] = new Circle(a, BallRadius);
        _circles[1] = new Circle(b, BallRadius);
        _shape?.InvalidateBounds();
    }

    private static float Normalize(float angle)
    {
        var turn = 2f * MathF.PI;
        var wrapped = angle % turn;
        return wrapped < 0f ? wrapped + turn : wrapped;
    }
}
