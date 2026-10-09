using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class FiringTimerTests
{
    [Fact]
    public void Waits_a_full_second_then_emits_at_each_boundary()
    {
        var timer = new FiringTimer();

        Assert.Empty(timer.Update(0f, Vector2.Zero, 0f));
        Assert.Empty(timer.Update(0.25f, Vector2.Zero, 0f));
        Assert.Empty(timer.Update(0.5f, Vector2.Zero, 0f));
        Assert.Single(timer.Update(0.25f, Vector2.Zero, 0f));
        Assert.Empty(timer.Update(0f, Vector2.Zero, 0f));
        Assert.Single(timer.Update(1f, Vector2.Zero, 0f));
    }

    [Fact]
    public void Crossing_a_boundary_keeps_fractional_remainder()
    {
        var timer = new FiringTimer();

        Assert.Empty(timer.Update(0.75f, Vector2.Zero, 0f));
        Assert.Single(timer.Update(0.5f, Vector2.Zero, 0f));
        Assert.Single(timer.Update(0.75f, Vector2.Zero, 0f));
    }

    [Theory]
    [InlineData(1, 2.5f)]
    [InlineData(10, 0.25f)]
    [InlineData(25, 0.1f)]
    [InlineData(150, 1f / 60f)]
    [InlineData(10000, 0.01f)]
    public void Emitted_count_is_floor_of_total_elapsed(int steps, float step)
    {
        var timer = new FiringTimer();
        var count = 0;
        var total = 0d;

        for (var i = 0; i < steps; i++)
        {
            total += step;
            count += timer.Update(step, Vector2.Zero, 0f).Count;
        }

        Assert.Equal((int)Math.Floor(total), count);
    }

    [Fact]
    public void Each_emission_captures_current_nose_and_heading()
    {
        var ship = new Ship(new Vector2(320, 240));
        var timer = new FiringTimer();
        Assert.Empty(timer.Update(0.5f, ship.Nose, ship.Heading));
        ship.Update(1f, 1f, 0.5f);

        var first = Assert.Single(timer.Update(0.5f, ship.Nose, ship.Heading));

        AssertPointNear(new Vector2(444, 240), first.Position);
        AssertPointNear(ship.Nose, first.Position);
        Assert.Equal(ship.Heading, first.Heading);
        ship.Update(1f, 1f, 0.5f);
        var second = Assert.Single(timer.Update(1f, ship.Nose, ship.Heading));
        AssertPointNear(ship.Nose, second.Position);
        Assert.Equal(ship.Heading, second.Heading);
        AssertPointNear(new Vector2(444, 240), first.Position);
        Assert.Equal(MathF.PI / 2f, first.Heading);
        Assert.NotSame(first, second);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Invalid_elapsed_is_rejected_without_changing_timer(float elapsed)
    {
        var timer = new FiringTimer();
        Assert.Empty(timer.Update(0.5f, Vector2.Zero, 0f));

        Assert.Throws<ArgumentOutOfRangeException>(() => timer.Update(elapsed, Vector2.Zero, 0f));

        Assert.Single(timer.Update(0.5f, Vector2.Zero, 0f));
    }

    private static void AssertPointNear(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 0.001f, $"Expected {expected}, actual {actual}");
}
