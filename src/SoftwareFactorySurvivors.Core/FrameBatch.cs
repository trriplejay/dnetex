using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>The GPU primitive a batch is drawn with.</summary>
public enum BatchKind
{
    /// <summary>Independent triangles, 3 vertices each (thick strokes, filled polygons and discs).</summary>
    Triangles,

    /// <summary>Independent 1px line segments, 2 vertices each (line strips).</summary>
    Lines,
}

/// <summary>One vertex of a batch: a screen position and its colour.</summary>
public readonly record struct BatchVertex(Vector2 Position, RgbColor Color);

/// <summary>
/// A run of consecutive submissions of one kind and layer: VertexCount vertices starting at Start in the
/// vertex list of that kind (TriangleVertices or LineVertices).
/// </summary>
public readonly record struct Batch(BatchKind Kind, int Layer, int Start, int VertexCount);

/// <summary>
/// Collects one frame's vector drawing into a few vertex lists so it can be drawn with one call per batch
/// instead of one call per stroke. Submissions keep their order: a change of primitive kind or layer starts a
/// new batch, so drawing the batches in order paints exactly what drawing each piece in turn would.
/// Thick strokes become the same triangles as the StrokeGeometry.Thicken strip, but as an independent triangle
/// list, so two strokes are never joined by bridging triangles. Reset keeps every buffer, so once a frame has
/// been built, building a frame of the same or smaller size allocates nothing.
/// </summary>
public sealed class FrameBatch
{
    /// <summary>Segments around a filled circle (as many as the per-piece FillCircle used).</summary>
    public const int CircleSegments = 16;

    // Closed unit circle: CircleSegments + 1 points, the last equal to the first.
    private static readonly Vector2[] UnitCircle = Enumerable.Range(0, CircleSegments + 1)
        .Select(i => new Vector2(MathF.Cos(i * MathF.Tau / CircleSegments), MathF.Sin(i * MathF.Tau / CircleSegments)))
        .ToArray();

    private BatchVertex[] _triangles = new BatchVertex[1024];
    private BatchVertex[] _lines = new BatchVertex[256];
    private Batch[] _batches = new Batch[64];
    private Vector2[] _scratch = new Vector2[64];
    private int _triangleCount;
    private int _lineCount;
    private int _batchCount;

    /// <summary>Layer stamped on later submissions; changing it starts a new batch even for the same kind.</summary>
    public int Layer { get; set; }

    public int BatchCount => _batchCount;

    public Batch GetBatch(int index) =>
        (uint)index < (uint)_batchCount ? _batches[index] : throw new ArgumentOutOfRangeException(nameof(index));

    /// <summary>Every triangle vertex of the frame, in submission order (3 per triangle).</summary>
    public ReadOnlySpan<BatchVertex> TriangleVertices => _triangles.AsSpan(0, _triangleCount);

    /// <summary>Every line vertex of the frame, in submission order (2 per segment).</summary>
    public ReadOnlySpan<BatchVertex> LineVertices => _lines.AsSpan(0, _lineCount);

    /// <summary>The vertices of one batch.</summary>
    public ReadOnlySpan<BatchVertex> GetVertices(Batch batch) =>
        (batch.Kind == BatchKind.Triangles ? _triangles : _lines).AsSpan(batch.Start, batch.VertexCount);

    /// <summary>Starts a new frame: forgets every submission but keeps the buffers for reuse.</summary>
    public void Reset()
    {
        _triangleCount = 0;
        _lineCount = 0;
        _batchCount = 0;
        Layer = 0;
    }

    /// <summary>A polyline width pixels wide, as the triangles of its StrokeGeometry.Thicken strip.</summary>
    public void AddThickStroke(ReadOnlySpan<Vector2> stroke, float width, RgbColor color)
    {
        if (stroke.Length < 2)
            return;

        var stripLength = stroke.Length * 2;
        if (_scratch.Length < stripLength)
            Array.Resize(ref _scratch, Math.Max(stripLength, _scratch.Length * 2));
        var strip = _scratch.AsSpan(0, stripLength);
        StrokeGeometry.ThickenInto(stroke, width, strip);

        // Strip triangle i is (i, i+1, i+2), the same vertices in the same order the GPU would use.
        var triangles = stripLength - 2;
        var start = Reserve(BatchKind.Triangles, triangles * 3);
        for (var i = 0; i < triangles; i++)
        {
            _triangles[start++] = new(strip[i], color);
            _triangles[start++] = new(strip[i + 1], color);
            _triangles[start++] = new(strip[i + 2], color);
        }
    }

    /// <summary>
    /// A closed convex outline (first point equals last), filled as a fan from its first point. Outlines with
    /// fewer than 4 points draw nothing.
    /// </summary>
    public void AddPolygon(ReadOnlySpan<Vector2> outline, RgbColor color)
    {
        if (outline.Length < 4)
            return;

        var start = Reserve(BatchKind.Triangles, (outline.Length - 3) * 3);
        for (var i = 1; i < outline.Length - 2; i++)
        {
            _triangles[start++] = new(outline[0], color);
            _triangles[start++] = new(outline[i], color);
            _triangles[start++] = new(outline[i + 1], color);
        }
    }

    /// <summary>A filled disc: the CircleSegments-sided outline around center, filled as a fan.</summary>
    public void AddCircle(Vector2 center, float radius, RgbColor color)
    {
        var first = center + radius * UnitCircle[0];
        var start = Reserve(BatchKind.Triangles, (UnitCircle.Length - 3) * 3);
        for (var i = 1; i < UnitCircle.Length - 2; i++)
        {
            _triangles[start++] = new(first, color);
            _triangles[start++] = new(center + radius * UnitCircle[i], color);
            _triangles[start++] = new(center + radius * UnitCircle[i + 1], color);
        }
    }

    /// <summary>A 1px polyline, as one line segment per pair of consecutive points.</summary>
    public void AddLineStrip(ReadOnlySpan<Vector2> points, RgbColor color)
    {
        if (points.Length < 2)
            return;

        var start = Reserve(BatchKind.Lines, (points.Length - 1) * 2);
        for (var i = 0; i < points.Length - 1; i++)
        {
            _lines[start++] = new(points[i], color);
            _lines[start++] = new(points[i + 1], color);
        }
    }

    /// <summary>Makes room for count vertices of kind, extending the last batch or opening a new one.</summary>
    private int Reserve(BatchKind kind, int count)
    {
        ref var buffer = ref kind == BatchKind.Triangles ? ref _triangles : ref _lines;
        ref var used = ref kind == BatchKind.Triangles ? ref _triangleCount : ref _lineCount;

        if (buffer.Length < used + count)
            Array.Resize(ref buffer, Math.Max(used + count, buffer.Length * 2));

        if (_batchCount > 0 && _batches[_batchCount - 1] is { } last && last.Kind == kind && last.Layer == Layer)
        {
            _batches[_batchCount - 1] = last with { VertexCount = last.VertexCount + count };
        }
        else
        {
            if (_batchCount == _batches.Length)
                Array.Resize(ref _batches, _batches.Length * 2);
            _batches[_batchCount++] = new(kind, Layer, used, count);
        }

        var start = used;
        used += count;
        return start;
    }
}
