using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Remembers VectorText.GetStrokes results so HUD text that doesn't change isn't rebuilt every frame. A lookup
/// with the same text, centre and height returns the very same strokes; any change computes them afresh.
/// </summary>
public sealed class TextStrokeCache
{
    // Text that changes often (the clock, the score) would otherwise grow the cache forever.
    private const int MaxEntries = 256;

    private readonly Dictionary<(string Text, Vector2 Center, float Height), IReadOnlyList<Vector2[]>> _entries = [];

    public int Count => _entries.Count;

    /// <summary>Strokes for text centred on center with capitals height pixels tall (see VectorText.GetStrokes).</summary>
    public IReadOnlyList<Vector2[]> GetStrokes(string text, Vector2 center, float height)
    {
        var key = (text, center, height);
        if (_entries.TryGetValue(key, out var strokes))
            return strokes;

        if (_entries.Count >= MaxEntries)
            _entries.Clear();

        strokes = VectorText.GetStrokes(text, center, height);
        _entries[key] = strokes;
        return strokes;
    }
}
