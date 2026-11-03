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
        ship.Update(new Vector2(1, 0), new Vector2(10_000, 240), 0.5f);

        var first = Assert.Single(timer.Update(0.5f, ship.Nose, ship.Heading));

        AssertPointNear(new Vector2(444, 240), first.Position);
        AssertPointNear(ship.Nose, first.Position);
        Assert.Equal(ship.Heading, first.Heading);
        ship.Update(new Vector2(1, 0), new Vector2(10_000, 240), 0.5f);
        var second = Assert.Single(timer.Update(1f, ship.Nose, ship.Heading));
        AssertPointNear(ship.Nose, second.Position);
        Assert.Equal(ship.Heading, second.Heading);
        AssertPointNear(new Vector2(444, 240), first.Position);
        Assert.Equal(MathF.PI / 2f, first.Heading);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Shot_poses_carry_over_progress_across_a_rate_change()
    {
        var timer = new FiringTimer();

        Assert.Empty(timer.Update(0.5f, new Vector2(1, 2), 0.1f, shotsPerSecond: 1f));
        Assert.Equal(0.5d, timer.Progress, precision: 6);

        // Half the way there already; at 2 shots/s another 0.25 s finishes this shot, posed at this frame's nose.
        var shot = Assert.Single(timer.Update(0.25f, new Vector2(3, 4), 0.2f, shotsPerSecond: 2f));
        Assert.Equal(new ShotPose(new Vector2(3, 4), 0.2f), shot);
        Assert.Equal(0d, timer.Progress, precision: 6);

        // Two whole shots in one frame both take the same frame's pose, and the remainder carries on.
        var shots = timer.Update(1.25f, new Vector2(5, 6), 0.3f, shotsPerSecond: 2f);
        Assert.Equal([new ShotPose(new Vector2(5, 6), 0.3f), new ShotPose(new Vector2(5, 6), 0.3f)], shots);
        Assert.Equal(0.5d, timer.Progress, precision: 6);
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
