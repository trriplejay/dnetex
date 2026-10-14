using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>A tiny line-drawn font, so text needs no font assets and matches the game's vector look.</summary>
public static class VectorText
{
    // Glyphs live in a box 0.6 wide by 1 tall, y growing downward from the top.
    private const float GlyphWidth = 0.6f;
    private const float Advance = 0.9f;

    private static readonly Dictionary<char, Vector2[][]> Glyphs = new()
    {
        [' '] = [],
        ['A'] = [[new(0, 1), new(0.3f, 0), new(0.6f, 1)], [new(0.12f, 0.6f), new(0.48f, 0.6f)]],
        ['B'] = [[new(0, 0), new(0, 1), new(0.5f, 1), new(0.6f, 0.88f), new(0.6f, 0.62f), new(0.5f, 0.5f), new(0, 0.5f)], [new(0, 0), new(0.45f, 0), new(0.55f, 0.1f), new(0.55f, 0.4f), new(0.45f, 0.5f)]],
        ['C'] = [[new(0.6f, 0), new(0, 0), new(0, 1), new(0.6f, 1)]],
        ['D'] = [[new(0, 0), new(0, 1), new(0.4f, 1), new(0.6f, 0.8f), new(0.6f, 0.2f), new(0.4f, 0), new(0, 0)]],
        ['E'] = [[new(0.6f, 0), new(0, 0), new(0, 1), new(0.6f, 1)], [new(0, 0.5f), new(0.45f, 0.5f)]],
        ['F'] = [[new(0.6f, 0), new(0, 0), new(0, 1)], [new(0, 0.5f), new(0.45f, 0.5f)]],
        ['G'] = [[new(0.6f, 0), new(0, 0), new(0, 1), new(0.6f, 1), new(0.6f, 0.55f), new(0.3f, 0.55f)]],
        ['H'] = [[new(0, 0), new(0, 1)], [new(0.6f, 0), new(0.6f, 1)], [new(0, 0.5f), new(0.6f, 0.5f)]],
        ['I'] = [[new(0.1f, 0), new(0.5f, 0)], [new(0.3f, 0), new(0.3f, 1)], [new(0.1f, 1), new(0.5f, 1)]],
        ['J'] = [[new(0.6f, 0), new(0.6f, 1), new(0, 1), new(0, 0.7f)]],
        ['K'] = [[new(0, 0), new(0, 1)], [new(0.6f, 0), new(0, 0.5f), new(0.6f, 1)]],
        ['L'] = [[new(0, 0), new(0, 1), new(0.6f, 1)]],
        ['M'] = [[new(0, 1), new(0, 0), new(0.3f, 0.5f), new(0.6f, 0), new(0.6f, 1)]],
        ['N'] = [[new(0, 1), new(0, 0), new(0.6f, 1), new(0.6f, 0)]],
        ['O'] = [[new(0, 0), new(0.6f, 0), new(0.6f, 1), new(0, 1), new(0, 0)]],
        ['P'] = [[new(0, 1), new(0, 0), new(0.6f, 0), new(0.6f, 0.5f), new(0, 0.5f)]],
        ['Q'] = [[new(0, 0), new(0.6f, 0), new(0.6f, 1), new(0, 1), new(0, 0)], [new(0.35f, 0.7f), new(0.6f, 1)]],
        ['R'] = [[new(0, 1), new(0, 0), new(0.6f, 0), new(0.6f, 0.5f), new(0, 0.5f)], [new(0.25f, 0.5f), new(0.6f, 1)]],
        ['S'] = [[new(0.6f, 0.1f), new(0.5f, 0), new(0.1f, 0), new(0, 0.1f), new(0, 0.4f), new(0.1f, 0.5f), new(0.5f, 0.5f), new(0.6f, 0.6f), new(0.6f, 0.9f), new(0.5f, 1), new(0.1f, 1), new(0, 0.9f)]],
        ['T'] = [[new(0, 0), new(0.6f, 0)], [new(0.3f, 0), new(0.3f, 1)]],
        ['U'] = [[new(0, 0), new(0, 1), new(0.6f, 1), new(0.6f, 0)]],
        ['V'] = [[new(0, 0), new(0.3f, 1), new(0.6f, 0)]],
        ['W'] = [[new(0, 0), new(0.15f, 1), new(0.3f, 0.5f), new(0.45f, 1), new(0.6f, 0)]],
        ['X'] = [[new(0, 0), new(0.6f, 1)], [new(0.6f, 0), new(0, 1)]],
        ['Y'] = [[new(0, 0), new(0.3f, 0.5f), new(0.6f, 0)], [new(0.3f, 0.5f), new(0.3f, 1)]],
        ['Z'] = [[new(0, 0), new(0.6f, 0), new(0, 1), new(0.6f, 1)]],
        ['0'] = [[new(0, 0), new(0.6f, 0), new(0.6f, 1), new(0, 1), new(0, 0)], [new(0.6f, 0), new(0, 1)]],
        ['1'] = [[new(0.15f, 0.2f), new(0.3f, 0), new(0.3f, 1)], [new(0.1f, 1), new(0.5f, 1)]],
        ['2'] = [[new(0, 0), new(0.6f, 0), new(0.6f, 0.5f), new(0, 0.5f), new(0, 1), new(0.6f, 1)]],
        ['3'] = [[new(0, 0), new(0.6f, 0), new(0.6f, 1), new(0, 1)], [new(0.1f, 0.5f), new(0.6f, 0.5f)]],
        ['4'] = [[new(0, 0), new(0, 0.5f), new(0.6f, 0.5f)], [new(0.6f, 0), new(0.6f, 1)]],
        ['5'] = [[new(0.6f, 0), new(0, 0), new(0, 0.5f), new(0.6f, 0.5f), new(0.6f, 1), new(0, 1)]],
        ['6'] = [[new(0.6f, 0), new(0, 0), new(0, 1), new(0.6f, 1), new(0.6f, 0.5f), new(0, 0.5f)]],
        ['7'] = [[new(0, 0), new(0.6f, 0), new(0.2f, 1)]],
        ['8'] = [[new(0, 0), new(0.6f, 0), new(0.6f, 1), new(0, 1), new(0, 0)], [new(0, 0.5f), new(0.6f, 0.5f)]],
        ['9'] = [[new(0.6f, 0.5f), new(0, 0.5f), new(0, 0), new(0.6f, 0), new(0.6f, 1), new(0, 1)]],
        [':'] = [[new(0.3f, 0.25f), new(0.3f, 0.35f)], [new(0.3f, 0.65f), new(0.3f, 0.75f)]],
        ['-'] = [[new(0.1f, 0.5f), new(0.5f, 0.5f)]],
        ['.'] = [[new(0.3f, 0.9f), new(0.3f, 1)]],
        ['_'] = [[new(0, 1), new(0.6f, 1)]],
        ['+'] = [[new(0.05f, 0.5f), new(0.55f, 0.5f)], [new(0.3f, 0.25f), new(0.3f, 0.75f)]],
        ['%'] = [[new(0.6f, 0), new(0, 1)], [new(0.05f, 0.05f), new(0.2f, 0.05f), new(0.2f, 0.25f), new(0.05f, 0.25f), new(0.05f, 0.05f)], [new(0.4f, 0.75f), new(0.55f, 0.75f), new(0.55f, 0.95f), new(0.4f, 0.95f), new(0.4f, 0.75f)]],
        ['/'] = [[new(0.55f, 0), new(0.05f, 1)]],
        ['>'] = [[new(0.1f, 0.2f), new(0.5f, 0.5f), new(0.1f, 0.8f)]],
    };

    /// <summary>Width in pixels of text drawn with capital letters height pixels tall.</summary>
    public static float MeasureWidth(string text, float height) =>
        text.Length == 0 ? 0f : ((text.Length - 1) * Advance + GlyphWidth) * height;

    /// <summary>Strokes for text centred on center, with capital letters height pixels tall.</summary>
    public static IReadOnlyList<Vector2[]> GetStrokes(string text, Vector2 center, float height)
    {
        var width = MeasureWidth(text, height);
        var origin = center - new Vector2(width / 2f, height / 2f);

        var strokes = new List<Vector2[]>();
        for (var i = 0; i < text.Length; i++)
        {
            if (!Glyphs.TryGetValue(char.ToUpperInvariant(text[i]), out var glyph))
                throw new ArgumentException($"No vector glyph for '{text[i]}'.", nameof(text));

            var offset = origin + new Vector2(i * Advance * height, 0);
            strokes.AddRange(glyph.Select(stroke => stroke.Select(p => offset + p * height).ToArray()));
        }
        return strokes;
    }
}
