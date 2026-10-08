namespace SoftwareFactorySurvivors.Core;

public readonly record struct RgbColor(byte R, byte G, byte B);

/// <summary>Colours for vertices of the ship's strokes, independent of the drawing host.</summary>
public static class ShipRendering
{
    public static readonly RgbColor HeadTip = new(200, 255, 0);
    public static readonly RgbColor Default = new(255, 255, 255);

    public static RgbColor ColorFor(int strokeIndex, int vertexIndex) =>
        strokeIndex == 2 && vertexIndex == 0 ? HeadTip : Default;
}
