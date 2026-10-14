using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Where the level-up cards sit on screen: side by side, centred, so drawing and clicking agree.</summary>
public static class UpgradeCards
{
    public static readonly Vector2 CardSize = new(300f, 190f);
    public const float Gap = 32f;

    /// <summary>Top-left corner of each of count cards centred on center.</summary>
    public static IReadOnlyList<Vector2> Layout(Vector2 center, int count)
    {
        var totalWidth = count * CardSize.X + (count - 1) * Gap;
        var left = center.X - totalWidth / 2f;
        var top = center.Y - CardSize.Y / 2f;
        return Enumerable.Range(0, count).Select(i => new Vector2(left + i * (CardSize.X + Gap), top)).ToArray();
    }

    /// <summary>Index of the card under point, or null if it misses them all.</summary>
    public static int? HitTest(Vector2 center, int count, Vector2 point)
    {
        var corners = Layout(center, count);
        for (var i = 0; i < corners.Count; i++)
        {
            var offset = point - corners[i];
            if (offset.X >= 0 && offset.Y >= 0 && offset.X <= CardSize.X && offset.Y <= CardSize.Y)
                return i;
        }
        return null;
    }
}
