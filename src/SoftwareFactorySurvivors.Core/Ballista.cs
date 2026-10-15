using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// A heavy bolt: a shaft four lasers long, laser-wide, with a triangular arrowhead at its leading end.
/// It flies at laser speed; every dimension scales with the projectile size upgrade.
/// </summary>
public sealed class Ballista : Projectile
{
    /// <summary>Shaft length at 100% projectile size, in lasers.</summary>
    public const float ShaftLengthInLasers = 4f;

    /// <summary>Arrowhead width (across its base) and length, in shaft widths.</summary>
    public const float HeadSizeInShaftWidths = 3f;

    private readonly ThickLine[] _lines = new ThickLine[1];
    private readonly Vector2[] _arrowhead = new Vector2[4];
    private readonly Vector2[][] _polygons;

    public Ballista(Vector2 position, float heading, float scale = 1f, int pierce = 0)
        : base(position, heading, 3 * Ship.Speed, scale, pierce)
    {
        _polygons = [_arrowhead];
        UpdateGeometry();
        _shape = new ProjectileShape(_lines, _polygons, []);
    }

    public float ShaftLength => ShaftLengthInLasers * Laser.BaseLength * Scale;
    public float ShaftWidth => Laser.BaseWidth * Scale;
    public float HeadWidth => HeadSizeInShaftWidths * ShaftWidth;
    public float HeadLength => HeadSizeInShaftWidths * ShaftWidth;

    /// <summary>The shaft's centre segment, from the tail at Position to the arrowhead's base.</summary>
    public Vector2[] GetShaft() => [Position, Position + Forward * ShaftLength];

    /// <summary>The arrowhead as a closed triangle: one wing, the tip, the other wing, back to the first wing.</summary>
    public Vector2[] GetArrowhead()
    {
        var headBase = Position + Forward * ShaftLength;
        var side = new Vector2(-Forward.Y, Forward.X) * (HeadWidth / 2f);
        var leftWing = headBase - side;
        return [leftWing, headBase + Forward * HeadLength, headBase + side, leftWing];
    }

    protected override ProjectileShape BuildShape() => _shape!;

    protected override void RefreshShape() => UpdateGeometry();

    private void UpdateGeometry()
    {
        var headBase = Position + Forward * ShaftLength;
        _lines[0] = new ThickLine(Position, headBase, ShaftWidth / 2f);

        var side = new Vector2(-Forward.Y, Forward.X) * (HeadWidth / 2f);
        var leftWing = headBase - side;
        _arrowhead[0] = leftWing;
        _arrowhead[1] = headBase + Forward * HeadLength;
        _arrowhead[2] = headBase + side;
        _arrowhead[3] = leftWing;

        _shape?.InvalidateBounds();
    }
}
