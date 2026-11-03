using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>How lightning strikes are drawn, independent of the drawing host.</summary>
public static class LightningRendering
{
    /// <summary>Cyan (#00ffff) for every lightning stroke.</summary>
    public static readonly RgbColor Color = new(0, 255, 255);

    /// <summary>Drawn width of lightning strokes in pixels.</summary>
    public const float StrokeWidth = 4f;

    /// <summary>Maximum perpendicular offset in pixels from the straight hop line.</summary>
    public const float MaxOffset = 18f;

    /// <summary>Default number of segments (or interior points count + 1) for a bolt.</summary>
    public const int DefaultPointCount = 6;

    /// <summary>
    /// Computes zig-zag points for drawing a lightning bolt segment from From to To.
    /// Returns points starting exactly at segment.From and ending exactly at segment.To.
    /// If From == To, returns [From, To].
    /// </summary>
    public static Vector2[] BoltPoints(LightningSegment segment, Random rng)
    {
        var buffer = new Vector2[DefaultPointCount];
        var count = BoltPoints(segment, rng, buffer.AsSpan());
        if (count == buffer.Length)
            return buffer;

        var result = new Vector2[count];
        Array.Copy(buffer, result, count);
        return result;
    }

    /// <summary>
    /// Computes zig-zag points for drawing a lightning bolt segment from From to To,
    /// filling into the destination span and returning the number of points written.
    /// Requires destination.Length >= 2.
    /// </summary>
    public static int BoltPoints(LightningSegment segment, Random rng, Span<Vector2> destination)
    {
        if (destination.Length < 2)
            throw new ArgumentException("Destination buffer must have length >= 2.", nameof(destination));

        var from = segment.From;
        var to = segment.To;

        var delta = to - from;
        var length = delta.Length();

        if (length <= 0.0001f)
        {
            destination[0] = from;
            destination[1] = to;
            return 2;
        }

        var count = Math.Min(destination.Length, DefaultPointCount);
        destination[0] = from;
        destination[count - 1] = to;

        var dir = delta / length;
        // Perpendicular vector (-dir.Y, dir.X)
        var perp = new Vector2(-dir.Y, dir.X);

        var stepT = 1f / (count - 1);
        for (var i = 1; i < count - 1; i++)
        {
            var t = i * stepT;
            var basePoint = from + delta * t;

            // Random perpendicular offset in [-MaxOffset, MaxOffset], but at least non-zero
            var offset = ((float)rng.NextDouble() * 2f - 1f) * MaxOffset;
            // Prevent offset being too close to zero so it's guaranteed off-line
            if (MathF.Abs(offset) < 2f)
                offset = offset < 0 ? -2f : 2f;

            // Clamp perpendicular offset
            offset = Math.Clamp(offset, -MaxOffset, MaxOffset);

            destination[i] = basePoint + perp * offset;
        }

        return count;
    }
}
