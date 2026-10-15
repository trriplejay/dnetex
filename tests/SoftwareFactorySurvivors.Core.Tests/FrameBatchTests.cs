using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class FrameBatchTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor White = new(255, 255, 255);

    // --- Reference geometry: what the per-piece Desktop helpers drew before batching. ---

    // DrawThickStroke: StrokeGeometry.Thicken, drawn as a TriangleStrip, whose triangle i is (i, i+1, i+2).
    private static Vector2[] StripTriangles(Vector2[] stroke, float width)
    {
        var strip = StrokeGeometry.Thicken(stroke, width);
        var triangles = new List<Vector2>();
        for (var i = 0; i < strip.Length - 2; i++)
            triangles.AddRange([strip[i], strip[i + 1], strip[i + 2]]);
        return [.. triangles];
    }

    // FillConvexPolygon: a fan from the first point of a closed outline.
    private static Vector2[] FanTriangles(Vector2[] outline)
    {
        var vertices = new List<Vector2>();
        if (outline.Length < 4)
            return [];
        for (var i = 1; i < outline.Length - 2; i++)
            vertices.AddRange([outline[0], outline[i], outline[i + 1]]);
        return [.. vertices];
    }

    // FillCircle: 16 segments, closed outline, then the same fan.
    private static Vector2[] CircleTriangles(Vector2 center, float radius)
    {
        const int segments = 16;
        var outline = Enumerable.Range(0, segments + 1)
            .Select(i => center + radius * new Vector2(MathF.Cos(i * MathF.Tau / segments), MathF.Sin(i * MathF.Tau / segments)))
            .ToArray();
        return FanTriangles(outline);
    }

    private static Vector2[] Positions(ReadOnlySpan<BatchVertex> vertices)
    {
        var positions = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
            positions[i] = vertices[i].Position;
        return positions;
    }

    private static readonly Vector2[] StrokeA = [new(10, 10), new(60, 10), new(60, 50)];
    private static readonly Vector2[] StrokeB = [new(100, 100), new(140, 120), new(180, 100), new(100, 100)];
    private static readonly Vector2[] StrokeC = [new(200, 20), new(240, 60)];
    private static readonly Vector2[] Square = [new(0, 0), new(20, 0), new(20, 20), new(0, 20), new(0, 0)];
    private static readonly Vector2[] Strip = [new(5, 5), new(25, 5), new(25, 15)];

    [Fact]
    public void Mixed_sequence_batches_split_at_kind_changes_and_match_per_piece_geometry()
    {
        var batch = new FrameBatch();
        batch.AddThickStroke(StrokeA, 3f, Red);
        batch.AddThickStroke(StrokeB, 2f, Red);
        batch.AddPolygon(Square, White);
        batch.AddLineStrip(Strip, White);
        batch.AddThickStroke(StrokeC, 4f, White);

        Assert.Equal(3, batch.BatchCount);
        var first = batch.GetBatch(0);
        var second = batch.GetBatch(1);
        var third = batch.GetBatch(2);
        Assert.Equal(BatchKind.Triangles, first.Kind);
        Assert.Equal(BatchKind.Lines, second.Kind);
        Assert.Equal(BatchKind.Triangles, third.Kind);

        // Batch 1: stroke A, stroke B, then the square, each exactly as the per-piece helpers drew it.
        Vector2[] expectedFirst = [.. StripTriangles(StrokeA, 3f), .. StripTriangles(StrokeB, 2f), .. FanTriangles(Square)];
        Assert.Equal(expectedFirst, Positions(batch.GetVertices(first)));

        // Batch 2: the line strip as independent segments.
        Vector2[] expectedLines = [Strip[0], Strip[1], Strip[1], Strip[2]];
        Assert.Equal(expectedLines, Positions(batch.GetVertices(second)));

        // Batch 3: stroke C, after the lines, in its own batch so painter's order is kept.
        Assert.Equal(StripTriangles(StrokeC, 4f), Positions(batch.GetVertices(third)));
        Assert.Equal(first.VertexCount, third.Start);

        Assert.All(batch.GetVertices(first)[..(StripTriangles(StrokeA, 3f).Length + StripTriangles(StrokeB, 2f).Length)].ToArray(),
            v => Assert.Equal(Red, v.Color));
        Assert.All(batch.GetVertices(third).ToArray(), v => Assert.Equal(White, v.Color));
    }

    [Fact]
    public void Adjacent_strokes_are_never_joined_by_a_bridging_triangle()
    {
        var batch = new FrameBatch();
        batch.AddThickStroke(StrokeA, 3f, Red);
        batch.AddThickStroke(StrokeC, 3f, Red);

        var a = StrokeGeometry.Thicken(StrokeA, 3f).ToHashSet();
        var c = StrokeGeometry.Thicken(StrokeC, 3f).ToHashSet();
        var vertices = Positions(batch.TriangleVertices);
        Assert.Equal(0, vertices.Length % 3);
        for (var t = 0; t < vertices.Length; t += 3)
        {
            var triangle = vertices.AsSpan(t, 3).ToArray();
            var touchesA = triangle.Any(a.Contains);
            var touchesC = triangle.Any(c.Contains);
            Assert.False(touchesA && touchesC, $"Triangle {t / 3} bridges the two strokes");
        }

        // Two strips concatenated into one strip would have 2 extra bridging triangles; a list has exactly the strips' own.
        var expectedTriangles = (StrokeA.Length * 2 - 2) + (StrokeC.Length * 2 - 2);
        Assert.Equal(expectedTriangles * 3, vertices.Length);
    }

    [Fact]
    public void Layer_change_starts_a_new_batch_even_for_the_same_kind()
    {
        var batch = new FrameBatch();
        batch.AddThickStroke(StrokeA, 3f, Red);
        batch.Layer = 1;
        batch.AddThickStroke(StrokeC, 3f, Red);
        batch.AddCircle(new(50, 50), 5f, Red);

        Assert.Equal(2, batch.BatchCount);
        Assert.Equal(0, batch.GetBatch(0).Layer);
        Assert.Equal(1, batch.GetBatch(1).Layer);
        Assert.Equal(StripTriangles(StrokeA, 3f).Length, batch.GetBatch(0).VertexCount);
    }

    [Fact]
    public void Circle_and_arrowhead_match_the_per_piece_fill_geometry()
    {
        var center = new Vector2(123.5f, 77.25f);
        const float radius = 6.5f;
        var arrowhead = new Ballista(new(300, 200), 0.7f, scale: 1.5f).GetArrowhead();

        var batch = new FrameBatch();
        batch.AddCircle(center, radius, White);
        var circleCount = batch.TriangleVertices.Length;
        batch.AddPolygon(arrowhead, White);

        Assert.Equal(1, batch.BatchCount);
        var vertices = Positions(batch.TriangleVertices);
        Assert.Equal(CircleTriangles(center, radius), vertices[..circleCount]);
        Assert.Equal(FanTriangles(arrowhead), vertices[circleCount..]);
        Assert.NotEmpty(FanTriangles(arrowhead));
    }

    [Fact]
    public void Too_short_shapes_add_nothing()
    {
        var batch = new FrameBatch();
        batch.AddThickStroke(new Vector2[] { new(1, 1) }, 3f, Red);
        batch.AddPolygon(new Vector2[] { new(0, 0), new(1, 0), new(0, 0) }, Red);
        batch.AddLineStrip(new Vector2[] { new(1, 1) }, Red);
        Assert.Equal(0, batch.BatchCount);
        Assert.Equal(0, batch.TriangleVertices.Length);
        Assert.Equal(0, batch.LineVertices.Length);
    }

    [Fact]
    public void Reset_forgets_submissions()
    {
        var batch = new FrameBatch();
        batch.AddThickStroke(StrokeA, 3f, Red);
        batch.AddLineStrip(Strip, Red);
        batch.Reset();
        Assert.Equal(0, batch.BatchCount);
        Assert.Equal(0, batch.TriangleVertices.Length);
        Assert.Equal(0, batch.LineVertices.Length);
    }

    private static void BuildFrame(FrameBatch batch, Vector2[][] strokes, Vector2[] arrowhead)
    {
        batch.Reset();
        foreach (var stroke in strokes)
            batch.AddThickStroke(stroke, 3f, Red);
        batch.Layer = 1;
        for (var i = 0; i < 50; i++)
            batch.AddCircle(new Vector2(i * 10f, 40f), 4f, White);
        batch.AddPolygon(arrowhead, White);
        batch.AddPolygon(Square, White);
        batch.Layer = 2;
        batch.AddLineStrip(Strip, White);
        batch.AddThickStroke(StrokeC, 2f, White);
    }

    [Fact]
    public void Warm_frame_of_strokes_circles_polygons_and_line_strips_allocates_nothing()
    {
        // Many long strokes so the warm-up must grow every buffer past its initial size.
        var strokes = Enumerable.Range(0, 300)
            .Select(s => Enumerable.Range(0, 40).Select(i => new Vector2(s + i * 3f, MathF.Sin(i) * 20f)).ToArray())
            .Append(StrokeA).Append(StrokeB)
            .ToArray();
        var arrowhead = new Ballista(new(300, 200), 0.7f).GetArrowhead();
        var batch = new FrameBatch();

        BuildFrame(batch, strokes, arrowhead); // warm-up
        BuildFrame(batch, strokes, arrowhead); // JIT of every path done

        var before = GC.GetAllocatedBytesForCurrentThread();
        BuildFrame(batch, strokes, arrowhead);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(batch.BatchCount >= 3);
    }

    [Fact]
    public void Smaller_warm_frame_allocates_nothing()
    {
        var batch = new FrameBatch();
        var big = Enumerable.Range(0, 100).Select(_ => StrokeB).ToArray();
        var arrowhead = new Ballista(new(300, 200), 0.7f).GetArrowhead();
        BuildFrame(batch, big, arrowhead);
        BuildFrame(batch, big, arrowhead);

        Vector2[][] small = [StrokeA];
        var before = GC.GetAllocatedBytesForCurrentThread();
        BuildFrame(batch, small, arrowhead);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
