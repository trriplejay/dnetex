using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>A segment drawn and hit-tested as a capsule of the given half-width around A to B.</summary>
public readonly record struct ThickLine(Vector2 A, Vector2 B, float HalfWidth);

/// <summary>A filled disc.</summary>
public readonly record struct Circle(Vector2 Center, float Radius);

/// <summary>
/// A box that holds a whole projectile shape (see ProjectileShape.GetBounds). It is only a quick reject: when it
/// can't reach an enemy's bounding circle the shape can't touch the enemy, but reaching it decides nothing.
/// </summary>
public readonly record struct ShapeBounds(Vector2 Min, Vector2 Max)
{
    // Absorbs float rounding in the transformed outline, so the reject never drops a real touch.
    private const float Slack = 0.01f;

    /// <summary>True when the box comes within radius of center, i.e. contact can't be ruled out.</summary>
    public bool Reaches(Vector2 center, float radius)
    {
        var nearest = Vector2.Clamp(center, Min, Max);
        var reach = radius + Slack;
        return Vector2.DistanceSquared(nearest, center) <= reach * reach;
    }

    /// <summary>
    /// True when the box comes within the enemy's bounding circle: its Position plus the furthest point of its
    /// outline from its centre (rotation keeps that distance, so it holds at every rotation).
    /// </summary>
    public bool Reaches(Enemy enemy) => Reaches(enemy.Position, enemy.Type.Radius);

    /// <summary>The furthest distance of any outline point from the outline's local origin.</summary>
    public static float OutlineRadius(IReadOnlyList<Vector2[]> outline)
    {
        var furthest = 0f;
        for (var i = 0; i < outline.Count; i++)
        {
            var stroke = outline[i];
            for (var j = 0; j < stroke.Length; j++)
                furthest = MathF.Max(furthest, stroke[j].Length());
        }
        return furthest;
    }
}

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
    private ShapeBounds? _bounds;

    /// <summary>True when any part of the shape touches any of the strokes, or lies inside a closed one.</summary>
    public bool Touches(IReadOnlyList<Vector2[]> strokes) =>
        Lines.Any(line => Collision.ThickSegmentTouches(line.A, line.B, line.HalfWidth, strokes))
        || Polygons.Any(polygon => Collision.StrokesTouch([polygon], strokes))
        || Circles.Any(circle => Collision.CircleTouches(circle.Center, circle.Radius, strokes));

    /// <summary>
    /// An axis-aligned box holding the whole shape: each line's endpoints padded by its half-width, each circle
    /// padded by its radius, and every polygon vertex (so anything inside a polygon is inside the box too).
    /// </summary>
    public ShapeBounds GetBounds() => _bounds ??= ComputeBounds();

    /// <summary>
    /// Clears the cached bounding box so the next call to <see cref="GetBounds"/> recomputes from the shape's geometry.
    /// Used when backing collections are updated in place without reallocating the shape.
    /// </summary>
    public void InvalidateBounds() => _bounds = null;

    private ShapeBounds ComputeBounds()
    {
        var min = new Vector2(float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity);
        for (var i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];
            var pad = new Vector2(line.HalfWidth);
            min = Vector2.Min(min, Vector2.Min(line.A, line.B) - pad);
            max = Vector2.Max(max, Vector2.Max(line.A, line.B) + pad);
        }
        for (var i = 0; i < Polygons.Count; i++)
        {
            var polygon = Polygons[i];
            for (var j = 0; j < polygon.Length; j++)
            {
                var point = polygon[j];
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
        for (var i = 0; i < Circles.Count; i++)
        {
            var circle = Circles[i];
            var pad = new Vector2(circle.Radius);
            min = Vector2.Min(min, circle.Center - pad);
            max = Vector2.Max(max, circle.Center + pad);
        }
        return new ShapeBounds(min, max);
    }

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
