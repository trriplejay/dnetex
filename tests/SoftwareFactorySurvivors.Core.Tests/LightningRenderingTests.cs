using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class LightningRenderingTests
{
    [Fact]
    public void Lightning_is_cyan_and_stroke_width_is_4px()
    {
        // CLM-3: The lightning bolt's stroke width is about 4 px (LightningRendering.StrokeWidth = 4f,
        // so more than the old 2 px), and its colour is cyan (0,255,255).
        Assert.True(LightningRendering.StrokeWidth > 2f);
        Assert.Equal(4f, LightningRendering.StrokeWidth);
        Assert.Equal(new RgbColor(0, 255, 255), LightningRendering.Color);
    }

    [Fact]
    public void Zero_length_hop_returns_exactly_the_two_endpoints_without_nan()
    {
        // CLM-4: A zero-length hop (From == To) makes BoltPoints return exactly the two endpoints,
        // with no NaN from normalising a zero vector.
        var pt = new Vector2(100f, 200f);
        var segment = new LightningSegment(pt, pt);
        var rng = new Random(42);

        var points = LightningRendering.BoltPoints(segment, rng);

        Assert.Equal(2, points.Length);
        Assert.Equal(pt, points[0]);
        Assert.Equal(pt, points[1]);
        Assert.False(float.IsNaN(points[0].X));
        Assert.False(float.IsNaN(points[0].Y));
        Assert.False(float.IsNaN(points[1].X));
        Assert.False(float.IsNaN(points[1].Y));

        Span<Vector2> buffer = stackalloc Vector2[8];
        var count = LightningRendering.BoltPoints(segment, rng, buffer);
        Assert.Equal(2, count);
        Assert.Equal(pt, buffer[0]);
        Assert.Equal(pt, buffer[1]);
    }

    [Theory]
    [InlineData(0f, 0f, 100f, 0f)]     // Horizontal
    [InlineData(0f, 0f, 0f, 100f)]     // Vertical
    [InlineData(10f, 20f, 150f, 200f)] // Diagonal
    [InlineData(200f, 100f, 50f, 30f)] // Negative diagonal
    public void Non_zero_hop_returns_more_than_two_points_with_endpoints_matching_and_interior_off_line(
        float fromX, float fromY, float toX, float toY)
    {
        // CLM-1: For any lightning hop (From, To) with From != To, LightningRendering.BoltPoints returns
        // a line of more than 2 points. Its first point equals From exactly, its last equals To exactly,
        // and at least one point in between lies off the straight From-To line, so the bolt zig-zags.
        var from = new Vector2(fromX, fromY);
        var to = new Vector2(toX, toY);
        var segment = new LightningSegment(from, to);
        var rng = new Random(12345);

        var points = LightningRendering.BoltPoints(segment, rng);

        Assert.True(points.Length > 2);
        Assert.Equal(from, points[0]);
        Assert.Equal(to, points[^1]);

        var line = to - from;
        var lineLen = line.Length();
        var lineDir = line / lineLen;

        var anyOffLine = false;
        const float epsilon = 0.5f;

        for (var i = 1; i < points.Length - 1; i++)
        {
            var p = points[i];
            var v = p - from;
            // Cross product in 2D gives perpendicular distance * lineLen
            var cross = MathF.Abs(v.X * lineDir.Y - v.Y * lineDir.X);
            if (cross > epsilon)
            {
                anyOffLine = true;
                break;
            }
        }

        Assert.True(anyOffLine, "At least one interior point should be offset from the straight line.");
    }

    [Fact]
    public void Points_stay_within_bounded_distance_and_projection_between_from_and_to()
    {
        // CLM-2: Every point of a BoltPoints line stays within a fixed, bounded distance of the From-To hop,
        // measured perpendicular to it, so the bolt never strays visibly away from the two points it joins.
        var maxBound = LightningRendering.MaxOffset;
        Assert.True(maxBound > 0f);

        var hops = new (Vector2 From, Vector2 To)[]
        {
            (new Vector2(0, 0), new Vector2(100, 0)),
            (new Vector2(0, 0), new Vector2(0, 100)),
            (new Vector2(10, 20), new Vector2(300, 400)),
            (new Vector2(-50, 120), new Vector2(250, -80)),
            (new Vector2(500, 500), new Vector2(10, 10))
        };

        for (var seed = 0; seed < 50; seed++)
        {
            var rng = new Random(seed);
            foreach (var (from, to) in hops)
            {
                var segment = new LightningSegment(from, to);
                var points = LightningRendering.BoltPoints(segment, rng);

                var line = to - from;
                var lineLen = line.Length();
                var lineDir = line / lineLen;

                foreach (var p in points)
                {
                    var v = p - from;
                    // Project v onto lineDir
                    var proj = Vector2.Dot(v, lineDir);
                    // Projection must stay between -0.01 and lineLen + 0.01
                    Assert.InRange(proj, -0.01f, lineLen + 0.01f);

                    // Perpendicular distance from line
                    var perpDist = MathF.Abs(v.X * lineDir.Y - v.Y * lineDir.X);
                    Assert.InRange(perpDist, 0f, maxBound + 0.001f);
                }
            }
        }
    }
}
