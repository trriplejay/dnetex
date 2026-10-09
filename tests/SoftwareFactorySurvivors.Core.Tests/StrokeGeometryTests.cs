using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class StrokeGeometryTests
{
    [Fact]
    public void Straight_line_becomes_a_strip_of_the_given_width()
    {
        var strip = StrokeGeometry.Thicken([new(0, -16), new(0, 16)], 4f);

        Assert.Equal(4, strip.Length);
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(4f, Vector2.Distance(strip[2 * i], strip[2 * i + 1]), precision: 5);
            Assert.Equal(0f, (strip[2 * i] + strip[2 * i + 1]).X / 2f, precision: 5);
        }
        Assert.Equal(-16f, strip[0].Y, precision: 5);
        Assert.Equal(16f, strip[3].Y, precision: 5);
    }

    [Fact]
    public void Each_input_point_maps_to_two_strip_vertices_centred_on_it()
    {
        var ship = new Ship(new Vector2(320, 240));

        foreach (var stroke in ship.GetStrokes())
        {
            var strip = StrokeGeometry.Thicken(stroke, 3f);
            Assert.Equal(stroke.Length * 2, strip.Length);
            for (var i = 0; i < stroke.Length; i++)
            {
                var centre = (strip[2 * i] + strip[2 * i + 1]) / 2f;
                Assert.True(Vector2.Distance(stroke[i], centre) < 0.001f);
                Assert.True(Vector2.Distance(strip[2 * i], strip[2 * i + 1]) >= 3f - 0.001f);
            }
        }
    }

    [Fact]
    public void Closed_loop_joins_without_a_seam()
    {
        var loop = new Ship(new Vector2(320, 240)).GetStrokes()[0];

        var strip = StrokeGeometry.Thicken(loop, 3f);

        Assert.True(Vector2.Distance(strip[0], strip[^2]) < 0.001f);
        Assert.True(Vector2.Distance(strip[1], strip[^1]) < 0.001f);
    }

    [Fact]
    public void Too_short_stroke_produces_nothing()
    {
        Assert.Empty(StrokeGeometry.Thicken([new(1, 1)], 3f));
    }
}
