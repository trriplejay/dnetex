using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ShipRenderingTests
{
    [Fact]
    public void Only_the_head_tip_vertex_is_exactly_lime()
    {
        var ship = new Ship(new Vector2(320, 240));
        var strokes = ship.GetStrokes();
        Assert.Equal(ship.Nose, strokes[2][0]);

        for (var strokeIndex = 0; strokeIndex < strokes.Count; strokeIndex++)
        {
            for (var vertexIndex = 0; vertexIndex < strokes[strokeIndex].Length; vertexIndex++)
            {
                var expected = strokeIndex == 2 && vertexIndex == 0
                    ? new RgbColor(200, 255, 0)
                    : new RgbColor(255, 255, 255);
                Assert.Equal(expected, ShipRendering.ColorFor(strokeIndex, vertexIndex));
            }
        }
    }
}
