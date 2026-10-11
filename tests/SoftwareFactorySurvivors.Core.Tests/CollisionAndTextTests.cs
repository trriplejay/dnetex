using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class CollisionAndTextTests
{
    private static readonly Vector2[] Square = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)];

    [Fact]
    public void Crossing_segments_touch_and_separate_ones_do_not()
    {
        Assert.True(Collision.SegmentsIntersect(new(0, 0), new(10, 10), new(0, 10), new(10, 0)));
        Assert.False(Collision.SegmentsIntersect(new(0, 0), new(10, 0), new(0, 5), new(10, 5)));
        Assert.True(Collision.SegmentsIntersect(new(0, 0), new(10, 0), new(10, 0), new(10, 5)));
    }

    [Fact]
    public void Shape_fully_inside_a_closed_outline_counts_as_touching()
    {
        Vector2[][] inner = [[new(4, 4), new(6, 6)]];

        Assert.True(Collision.StrokesTouch(inner, [Square]));
        Assert.True(Collision.StrokesTouch([Square], inner));
        Assert.False(Collision.StrokesTouch([[new(20, 20), new(30, 30)]], [Square]));
    }

    [Fact]
    public void Vector_text_is_centred_and_scaled()
    {
        var strokes = VectorText.GetStrokes("GAME OVER", new Vector2(500, 400), 64f);
        var points = strokes.SelectMany(s => s).ToArray();

        Assert.Equal(400f - 32f, points.Min(p => p.Y), precision: 3);
        Assert.Equal(400f + 32f, points.Max(p => p.Y), precision: 3);
        Assert.Equal(500f, (points.Min(p => p.X) + points.Max(p => p.X)) / 2f, precision: 3);
        Assert.Throws<ArgumentException>(() => VectorText.GetStrokes("?", Vector2.Zero, 10f));
    }

    [Fact]
    public void Vector_text_draws_every_letter_digit_and_hud_symbol()
    {
        foreach (var c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789:._-+%/>")
            Assert.NotEmpty(VectorText.GetStrokes(c.ToString(), Vector2.Zero, 10f));
    }
}
