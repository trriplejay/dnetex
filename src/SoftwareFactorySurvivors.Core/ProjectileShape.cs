using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>A segment drawn and hit-tested as a capsule of the given half-width around A to B.</summary>
public readonly record struct ThickLine(Vector2 A, Vector2 B, float HalfWidth);

/// <summary>A filled disc.</summary>
public readonly record struct Circle(Vector2 Center, float Radius);

/// <summary>
/// Everything a projectile is made of, in screen coordinates: thick lines, filled closed polygons (first point
/// equals last) and filled circles. The same shape is drawn, hit-tested against enemies and checked for expiry,
/// so what the player sees is what hits.
/// </summary>
public sealed record ProjectileShape(
    IReadOnlyList<ThickLine> Lines,
    IReadOnlyList<Vector2[]> Polygons,
    IReadOnlyList<Circle> Circles)
{
    /// <summary>True when any part of the shape touches any of the strokes, or lies inside a closed one.</summary>
    public bool Touches(IReadOnlyList<Vector2[]> strokes) =>
        Lines.Any(line => Collision.ThickSegmentTouches(line.A, line.B, line.HalfWidth, strokes))
        || Polygons.Any(polygon => Collision.StrokesTouch([polygon], strokes))
        || Circles.Any(circle => Collision.CircleTouches(circle.Center, circle.Radius, strokes));

    /// <summary>
    /// True when some part of the shape lies inside the closed rectangle [0,width] × [0,height]. A line counts by
    /// its centre segment (as the laser always has), a polygon by its edges or by covering the rectangle, and a
    /// circle by its whole disc.
    /// </summary>
    public bool Overlaps(float width, float height) =>
        Lines.Any(line => SegmentOverlaps(line.A, line.B, width, height))
        || Polygons.Any(polygon => PolygonOverlaps(polygon, width, height))
        || Circles.Any(circle => CircleOverlaps(circle, width, height));

    private static bool PolygonOverlaps(Vector2[] polygon, float width, float height)
    {
        for (var i = 0; i < polygon.Length - 1; i++)
        {
            if (SegmentOverlaps(polygon[i], polygon[i + 1], width, height))
                return true;
        }
        return Collision.ContainsPoint(polygon, Vector2.Zero);
    }

    private static bool CircleOverlaps(Circle circle, float width, float height)
    {
        var nearest = new Vector2(Math.Clamp(circle.Center.X, 0f, width), Math.Clamp(circle.Center.Y, 0f, height));
        return Vector2.Distance(nearest, circle.Center) <= circle.Radius;
    }

    private static bool SegmentOverlaps(Vector2 a, Vector2 b, float width, float height)
    {
        var direction = b - a;
        var enter = 0f;
        var exit = 1f;
        return IntersectsAxis(a.X, direction.X, width, ref enter, ref exit)
            && IntersectsAxis(a.Y, direction.Y, height, ref enter, ref exit);
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
