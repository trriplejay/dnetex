using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ShipRenderingTests
{
    [Fact]
    public void Ship_is_lime_green()
    {
        Assert.Equal(new RgbColor(200, 255, 0), ShipRendering.Color);
    }

    [Fact]
    public void Ship_strokes_are_2px()
    {
        Assert.Equal(2f, ShipRendering.StrokeWidth);
    }
}
