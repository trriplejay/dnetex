using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Turns 1px polylines into triangle strips with real width, since GPU lines are always 1px.</summary>
public static class StrokeGeometry
{
    /// <summary>
    /// Returns a triangle strip of 2 vertices per input point: point i becomes strip vertices 2i and 2i+1,
    /// offset to either side by half of width. Closed strokes (first point equals last) join seamlessly.
    /// </summary>
    public static Vector2[] Thicken(Vector2[] stroke, float width)
    {
        if (stroke.Length < 2)
            return [];

        var halfWidth = width / 2f;
        var closed = stroke.Length > 2 && stroke[0] == stroke[^1];
        var strip = new Vector2[stroke.Length * 2];

        for (var i = 0; i < stroke.Length; i++)
        {
            var previous = i > 0 ? stroke[i - 1] : closed ? stroke[^2] : stroke[i];
            var next = i < stroke.Length - 1 ? stroke[i + 1] : closed ? stroke[1] : stroke[i];
            var offset = MiterOffset(previous, stroke[i], next, halfWidth);
            strip[2 * i] = stroke[i] + offset;
            strip[2 * i + 1] = stroke[i] - offset;
        }

        return strip;
    }

    private static Vector2 MiterOffset(Vector2 previous, Vector2 point, Vector2 next, float halfWidth)
    {
        var incoming = SafeNormalize(point - previous);
        var outgoing = SafeNormalize(next - point);
        if (incoming == Vector2.Zero) incoming = outgoing;
        if (outgoing == Vector2.Zero) outgoing = incoming;

        var normal = Perpendicular(incoming);
        var miter = SafeNormalize(Perpendicular(incoming + outgoing));
        if (miter == Vector2.Zero)
            return normal * halfWidth;

        // Lengthen the offset at bends so the strip keeps its width, capped to avoid spikes at sharp corners.
        var scale = MathF.Min(1f / MathF.Max(Vector2.Dot(miter, normal), 0.0001f), 2f);
        return miter * halfWidth * scale;
    }

    private static Vector2 Perpendicular(Vector2 v) => new(-v.Y, v.X);

    private static Vector2 SafeNormalize(Vector2 v) =>
        v.LengthSquared() > 0f ? Vector2.Normalize(v) : Vector2.Zero;
}
