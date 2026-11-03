using System.Numerics;
using Xunit;

namespace SoftwareFactorySurvivors.Core.Tests;

public class UpgradeCardsTests
{
    private static readonly Vector2 Center = new(640f, 360f);
    private const float ViewportWidth = 1280f;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void For_counts_1_to_5_cards_stay_within_viewport_and_do_not_intersect(int count)
    {
        var corners = UpgradeCards.Layout(Center, count, ViewportWidth);

        Assert.Equal(count, corners.Count);

        for (var i = 0; i < count; i++)
        {
            var c = corners[i];
            Assert.True(c.X >= 0f, $"Card {i} X ({c.X}) must be >= 0");
            Assert.True(c.X + UpgradeCards.CardSize.X <= ViewportWidth, $"Card {i} right edge ({c.X + UpgradeCards.CardSize.X}) must be <= {ViewportWidth}");

            var cardCenter = c + UpgradeCards.CardSize / 2f;
            Assert.Equal(i, UpgradeCards.HitTest(Center, count, cardCenter, ViewportWidth));

            for (var j = i + 1; j < count; j++)
            {
                var other = corners[j];
                var noOverlapX = c.X + UpgradeCards.CardSize.X <= other.X || other.X + UpgradeCards.CardSize.X <= c.X;
                var noOverlapY = c.Y + UpgradeCards.CardSize.Y <= other.Y || other.Y + UpgradeCards.CardSize.Y <= c.Y;
                Assert.True(noOverlapX || noOverlapY, $"Card {i} and Card {j} must not intersect");
            }
        }
    }

    [Fact]
    public void HitTest_between_cards_and_outside_returns_null()
    {
        const int count = 5;
        var corners = UpgradeCards.Layout(Center, count, ViewportWidth);

        // Between cards horizontally on row 0
        var gapBetween0And1 = new Vector2(corners[0].X + UpgradeCards.CardSize.X + UpgradeCards.Gap / 2f, corners[0].Y + UpgradeCards.CardSize.Y / 2f);
        Assert.Null(UpgradeCards.HitTest(Center, count, gapBetween0And1, ViewportWidth));

        // Between rows vertically
        var gapBetweenRows = new Vector2(Center.X, corners[0].Y + UpgradeCards.CardSize.Y + UpgradeCards.Gap / 2f);
        Assert.Null(UpgradeCards.HitTest(Center, count, gapBetweenRows, ViewportWidth));

        // Outside left
        Assert.Null(UpgradeCards.HitTest(Center, count, new Vector2(-10f, Center.Y), ViewportWidth));

        // Outside right
        Assert.Null(UpgradeCards.HitTest(Center, count, new Vector2(ViewportWidth + 10f, Center.Y), ViewportWidth));

        // Outside top
        Assert.Null(UpgradeCards.HitTest(Center, count, new Vector2(Center.X, -50f), ViewportWidth));

        // Outside bottom
        Assert.Null(UpgradeCards.HitTest(Center, count, new Vector2(Center.X, 1000f), ViewportWidth));
    }

    [Fact]
    public void Layout_with_zero_or_negative_count_returns_empty()
    {
        Assert.Empty(UpgradeCards.Layout(Center, 0, ViewportWidth));
        Assert.Empty(UpgradeCards.Layout(Center, -1, ViewportWidth));
        Assert.Null(UpgradeCards.HitTest(Center, 0, Center, ViewportWidth));
    }

    [Fact]
    public void Layout_defaults_viewport_width_when_omitted()
    {
        var cornersWithDefault = UpgradeCards.Layout(Center, 5);
        var cornersExplicit = UpgradeCards.Layout(Center, 5, ViewportWidth);

        Assert.Equal(cornersExplicit, cornersWithDefault);
    }
}
