namespace SoftwareFactorySurvivors.Core;

public readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>How the ship's strokes are drawn, independent of the drawing host.</summary>
public static class ShipRendering
{
    /// <summary>Lime green (#c8ff00) for every stroke of the ship.</summary>
    public static readonly RgbColor Color = new(200, 255, 0);

    /// <summary>Drawn width of the ship's strokes in pixels.</summary>
    public const float StrokeWidth = 4.5f;
}
