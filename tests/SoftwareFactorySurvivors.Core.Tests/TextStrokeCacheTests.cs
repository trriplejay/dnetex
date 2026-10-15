using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class TextStrokeCacheTests
{
    private static readonly Vector2 Center = new(320, 40);

    private static void AssertSameStrokes(IReadOnlyList<Vector2[]> expected, IReadOnlyList<Vector2[]> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], actual[i]);
    }

    [Fact]
    public void Cached_strokes_equal_VectorText_strokes_for_the_key()
    {
        var cache = new TextStrokeCache();
        AssertSameStrokes(VectorText.GetStrokes("SCORE 120", Center, 16f), cache.GetStrokes("SCORE 120", Center, 16f));
    }

    [Fact]
    public void Same_key_returns_the_same_strokes_without_allocating()
    {
        var cache = new TextStrokeCache();
        var text = "LV 3";
        var first = cache.GetStrokes(text, Center, 24f);
        cache.GetStrokes(text, Center, 24f); // JIT the hit path

        var before = GC.GetAllocatedBytesForCurrentThread();
        var second = cache.GetStrokes(text, Center, 24f);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Same(first, second);
        Assert.Equal(0, allocated);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Changed_text_position_or_height_recomputes()
    {
        var cache = new TextStrokeCache();
        var original = cache.GetStrokes("00:01", Center, 32f);

        var changedText = cache.GetStrokes("00:02", Center, 32f);
        Assert.NotSame(original, changedText);
        AssertSameStrokes(VectorText.GetStrokes("00:02", Center, 32f), changedText);

        var moved = cache.GetStrokes("00:01", Center + new Vector2(5, 0), 32f);
        Assert.NotSame(original, moved);
        AssertSameStrokes(VectorText.GetStrokes("00:01", Center + new Vector2(5, 0), 32f), moved);

        var resized = cache.GetStrokes("00:01", Center, 16f);
        Assert.NotSame(original, resized);
        AssertSameStrokes(VectorText.GetStrokes("00:01", Center, 16f), resized);

        // The original key still yields the original strokes.
        AssertSameStrokes(VectorText.GetStrokes("00:01", Center, 32f), cache.GetStrokes("00:01", Center, 32f));
    }

    [Fact]
    public void Cache_stays_bounded_when_text_keeps_changing()
    {
        var cache = new TextStrokeCache();
        for (var i = 0; i < 2000; i++)
            cache.GetStrokes($"SCORE {i}", Center, 16f);
        Assert.True(cache.Count <= 256, $"Cache grew to {cache.Count}");
    }
}
