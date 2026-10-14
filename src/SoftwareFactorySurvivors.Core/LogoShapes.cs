using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Simplified line-art versions of AI company logos, each centred on (0,0) and fitting inside the given radius.
/// Screen Y grows downward. Closed strokes repeat their first point at the end.
/// </summary>
public static class LogoShapes
{
    /// <summary>Gemini: a four-pointed sparkle whose sides curve inward.</summary>
    public static Vector2[][] Gemini(float r)
    {
        // Each side is a quadratic curve between two tips, pulled toward the centre by a control point
        // just off the centre in that side's quadrant, which gives the logo's plump concave edges.
        const float controlPull = 0.15f;
        const int segmentsPerSide = 12;
        Vector2[] tips = [new(0, -r), new(r, 0), new(0, r), new(-r, 0)];
        var points = new List<Vector2>();
        for (var side = 0; side < tips.Length; side++)
        {
            var from = tips[side];
            var to = tips[(side + 1) % tips.Length];
            var control = (from + to) * controlPull;
            for (var i = 0; i < segmentsPerSide; i++)
            {
                var t = (float)i / segmentsPerSide;
                var u = 1f - t;
                points.Add(u * u * from + 2f * u * t * control + t * t * to);
            }
        }
        return [Close(points)];
    }

    /// <summary>Claude: a starburst of uneven rays radiating from a small open centre.</summary>
    public static Vector2[][] Claude(float r)
    {
        float[] lengths = [1f, 0.78f, 0.92f, 0.7f, 0.97f, 0.8f, 0.9f, 0.72f, 1f, 0.82f, 0.88f];
        return lengths.Select((length, i) =>
        {
            var direction = Direction(MathF.Tau * i / lengths.Length - MathF.PI / 2f);
            return new[] { direction * r * 0.22f, direction * r * length };
        }).ToArray();
    }

    /// <summary>ChatGPT: a six-petal knot of rounded links twisting around a hexagonal centre.</summary>
    public static Vector2[][] ChatGpt(float r) =>
        Enumerable.Range(0, 6).Select(i =>
        {
            var angle = MathF.Tau * i / 6f;
            return Ellipse(Direction(angle) * r * 0.42f, r * 0.55f, r * 0.24f, angle + MathF.PI / 3f, 24);
        }).ToArray();

    /// <summary>Cursor: an isometric cube, a hexagon with three edges meeting at the centre.</summary>
    public static Vector2[][] Cursor(float r)
    {
        var corners = Enumerable.Range(0, 6).Select(i => Direction(MathF.Tau * i / 6f - MathF.PI / 2f) * r).ToArray();
        return
        [
            Close(corners),
            [Vector2.Zero, corners[0]],
            [Vector2.Zero, corners[2]],
            [Vector2.Zero, corners[4]],
        ];
    }

    /// <summary>Grok: a ring cut by a diagonal slash that runs out past its top-right edge.</summary>
    public static Vector2[][] Grok(float r) =>
    [
        Ellipse(Vector2.Zero, r * 0.68f, r * 0.68f, 0f, 32),
        [new Vector2(-0.55f, 0.55f) * r, new Vector2(0.7f, -0.7f) * r],
    ];

    /// <summary>Copilot (GitHub): a rounded pilot's head wearing two big goggles.</summary>
    public static Vector2[][] Copilot(float r) =>
    [
        Ellipse(Vector2.Zero, r, r * 0.78f, 0f, 32),
        Ellipse(new Vector2(-0.4f, -0.1f) * r, r * 0.3f, r * 0.26f, 0f, 20),
        Ellipse(new Vector2(0.4f, -0.1f) * r, r * 0.3f, r * 0.26f, 0f, 20),
        [new Vector2(-0.15f, 0.32f) * r, new Vector2(-0.15f, 0.52f) * r],
        [new Vector2(0.15f, 0.32f) * r, new Vector2(0.15f, 0.52f) * r],
    ];

    /// <summary>Perplexity: a central stem through a crossbar box, with folded wings above and pages below.</summary>
    public static Vector2[][] Perplexity(float r) =>
    [
        [new Vector2(0, -0.95f) * r, new Vector2(0, 0.95f) * r],
        Close([new Vector2(-0.68f, -0.3f) * r, new Vector2(0.68f, -0.3f) * r, new Vector2(0.68f, 0.3f) * r, new Vector2(-0.68f, 0.3f) * r]),
        [new Vector2(-0.68f, -0.68f) * r, new Vector2(0, -0.3f) * r, new Vector2(0.68f, -0.68f) * r],
        [new Vector2(-0.68f, -0.68f) * r, new Vector2(-0.68f, -0.3f) * r],
        [new Vector2(0.68f, -0.68f) * r, new Vector2(0.68f, -0.3f) * r],
        [new Vector2(0, 0.3f) * r, new Vector2(-0.68f, 0.68f) * r, new Vector2(-0.68f, 0.3f) * r],
        [new Vector2(0, 0.3f) * r, new Vector2(0.68f, 0.68f) * r, new Vector2(0.68f, 0.3f) * r],
    ];

    /// <summary>Hugging Face: the hugging emoji, a round smiling face with two hands raised at the bottom.</summary>
    public static Vector2[][] HuggingFace(float r)
    {
        const int smileSegments = 12;
        var smile = Enumerable.Range(0, smileSegments + 1).Select(i =>
        {
            var angle = MathF.PI * (0.15f + 0.7f * i / smileSegments);
            return new Vector2(0, 0.05f) * r + Direction(angle) * r * 0.42f;
        }).ToArray();

        return
        [
            Ellipse(Vector2.Zero, r * 0.82f, r * 0.82f, 0f, 28),
            Ellipse(new Vector2(-0.3f, -0.22f) * r, r * 0.1f, r * 0.13f, 0f, 8),
            Ellipse(new Vector2(0.3f, -0.22f) * r, r * 0.1f, r * 0.13f, 0f, 8),
            smile,
            Ellipse(new Vector2(-0.5f, 0.55f) * r, r * 0.24f, r * 0.17f, -0.5f, 12),
            Ellipse(new Vector2(0.5f, 0.55f) * r, r * 0.24f, r * 0.17f, 0.5f, 12),
        ];
    }

    /// <summary>Meta: a wide, looping infinity sign with tall rounded lobes.</summary>
    public static Vector2[][] Meta(float r)
    {
        const int segments = 64;
        var points = new List<Vector2>();
        for (var i = 0; i < segments; i++)
        {
            // Lemniscate of Bernoulli, stretched vertically so the lobes stand taller than the classic shape.
            var t = MathF.Tau * i / segments;
            var denominator = 1f + MathF.Sin(t) * MathF.Sin(t);
            points.Add(new Vector2(r * MathF.Cos(t) / denominator, 1.5f * r * MathF.Sin(t) * MathF.Cos(t) / denominator));
        }
        return [Close(points)];
    }

    /// <summary>Apple Intelligence: one continuous line looping around a ring in six petals.</summary>
    public static Vector2[][] AppleIntelligence(float r)
    {
        const int segments = 96;
        var points = new List<Vector2>();
        for (var i = 0; i < segments; i++)
        {
            // A large circle plus a small counter-rotating one traces six inward loops.
            var t = MathF.Tau * i / segments;
            points.Add(Direction(t) * r * 0.7f + Direction(-5f * t) * r * 0.3f);
        }
        return [Close(points)];
    }

    private static Vector2 Direction(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    private static Vector2[] Close(IEnumerable<Vector2> points)
    {
        var list = points.ToList();
        list.Add(list[0]);
        return list.ToArray();
    }

    private static Vector2[] Ellipse(Vector2 center, float radiusX, float radiusY, float rotation, int segments)
    {
        var turn = Matrix3x2.CreateRotation(rotation);
        return Close(Enumerable.Range(0, segments).Select(i =>
        {
            var t = MathF.Tau * i / segments;
            return center + Vector2.Transform(new Vector2(radiusX * MathF.Cos(t), radiusY * MathF.Sin(t)), turn);
        }));
    }
}
