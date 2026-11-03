using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Where the level-up cards sit on screen: side by side, centred, so drawing and clicking agree.</summary>
public static class UpgradeCards
{
    public static readonly Vector2 CardSize = new(300f, 190f);
    public const float Gap = 32f;

    /// <summary>Top-left corner of each of count cards centred on center and fitted within viewportWidth.</summary>
    public static IReadOnlyList<Vector2> Layout(Vector2 center, int count, float viewportWidth = 1280f)
    {
        if (count <= 0)
            return [];

        if (viewportWidth <= 0f)
            viewportWidth = center.X > 0f ? center.X * 2f : 1280f;

        var maxCardsPerRow = Math.Max(1, (int)MathF.Floor((viewportWidth + Gap) / (CardSize.X + Gap)));
        var rowCount = (int)MathF.Ceiling((float)count / maxCardsPerRow);

        var totalHeight = rowCount * CardSize.Y + (rowCount - 1) * Gap;
        var startTop = center.Y - totalHeight / 2f;

        var baseCardsPerRow = count / rowCount;
        var extraCards = count % rowCount;

        var result = new Vector2[count];
        var cardIndex = 0;

        for (var row = 0; row < rowCount; row++)
        {
            var cardsInRow = baseCardsPerRow + (row < extraCards ? 1 : 0);
            var rowWidth = cardsInRow * CardSize.X + (cardsInRow - 1) * Gap;
            var rowLeft = center.X - rowWidth / 2f;
            var rowTop = startTop + row * (CardSize.Y + Gap);

            for (var col = 0; col < cardsInRow; col++)
            {
                result[cardIndex++] = new Vector2(rowLeft + col * (CardSize.X + Gap), rowTop);
            }
        }

        return result;
    }

    /// <summary>Index of the card under point, or null if it misses them all.</summary>
    public static int? HitTest(Vector2 center, int count, Vector2 point, float viewportWidth = 1280f)
    {
        var corners = Layout(center, count, viewportWidth);
        for (var i = 0; i < corners.Count; i++)
        {
            var offset = point - corners[i];
            if (offset.X >= 0 && offset.Y >= 0 && offset.X <= CardSize.X && offset.Y <= CardSize.Y)
                return i;
        }
        return null;
    }
}
