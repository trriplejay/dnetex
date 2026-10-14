namespace SoftwareFactorySurvivors.Core;

/// <summary>How enemies are drawn, independent of the drawing host.</summary>
public static class EnemyRendering
{
    /// <summary>White outlines for every enemy.</summary>
    public static readonly RgbColor Color = new(255, 255, 255);

    /// <summary>Drawn width of enemy outlines in pixels, matching the ship's strokes.</summary>
    public const float StrokeWidth = ShipRendering.StrokeWidth;
}
