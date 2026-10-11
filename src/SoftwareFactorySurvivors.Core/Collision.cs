using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Contact tests between drawn strokes, so hits match what the player sees.</summary>
public static class Collision
{
    /// <summary>
    /// True when any segment of one set crosses any segment of the other, or when a stroke of one set
    /// starts inside a closed stroke of the other (one shape fully inside the other).
    /// </summary>
    public static bool StrokesTouch(IReadOnlyList<Vector2[]> first, IReadOnlyList<Vector2[]> second)
    {
        foreach (var a in first)
        foreach (var b in second)
        {
            for (var i = 0; i < a.Length - 1; i++)
            for (var j = 0; j < b.Length - 1; j++)
            {
                if (SegmentsIntersect(a[i], a[i + 1], b[j], b[j + 1]))
                    return true;
            }
        }

        return first.Any(a => a.Length > 0 && second.Any(b => ContainsPoint(b, a[0])))
            || second.Any(b => b.Length > 0 && first.Any(a => ContainsPoint(a, b[0])));
    }

    /// <summary>
    /// True when a thick segment (a capsule of the given half-width around a to b) touches any of the strokes,
    /// or lies inside a closed one.
    /// </summary>
    public static bool ThickSegmentTouches(Vector2 a, Vector2 b, float halfWidth, IReadOnlyList<Vector2[]> strokes)
    {
        foreach (var stroke in strokes)
        {
            for (var i = 0; i < stroke.Length - 1; i++)
            {
                if (SegmentDistance(a, b, stroke[i], stroke[i + 1]) <= halfWidth)
                    return true;
            }
            if (ContainsPoint(stroke, a))
                return true;
        }
        return false;
    }

    public static float SegmentDistance(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        if (SegmentsIntersect(p1, p2, q1, q2))
            return 0f;

        return MathF.Min(
            MathF.Min(PointSegmentDistance(p1, q1, q2), PointSegmentDistance(p2, q1, q2)),
            MathF.Min(PointSegmentDistance(q1, p1, p2), PointSegmentDistance(q2, p1, p2)));
    }

    public static float PointSegmentDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared();
        var t = lengthSquared == 0f ? 0f : Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, a + ab * t);
    }

    public static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
    {
        var d1 = Cross(q2 - q1, p1 - q1);
        var d2 = Cross(q2 - q1, p2 - q1);
        var d3 = Cross(p2 - p1, q1 - p1);
        var d4 = Cross(p2 - p1, q2 - p1);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        return (d1 == 0 && OnSegment(q1, q2, p1))
            || (d2 == 0 && OnSegment(q1, q2, p2))
            || (d3 == 0 && OnSegment(p1, p2, q1))
            || (d4 == 0 && OnSegment(p1, p2, q2));
    }

    /// <summary>Even-odd test against a closed stroke (first point equals last); open strokes contain nothing.</summary>
    public static bool ContainsPoint(Vector2[] stroke, Vector2 point)
    {
        if (stroke.Length < 4 || stroke[0] != stroke[^1])
            return false;

        var inside = false;
        for (int i = 0, j = stroke.Length - 2; i < stroke.Length - 1; j = i++)
        {
            var a = stroke[i];
            var b = stroke[j];
            if ((a.Y > point.Y) != (b.Y > point.Y)
                && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p) =>
        p.X >= MathF.Min(a.X, b.X) && p.X <= MathF.Max(a.X, b.X)
        && p.Y >= MathF.Min(a.Y, b.Y) && p.Y <= MathF.Max(a.Y, b.Y);
}
