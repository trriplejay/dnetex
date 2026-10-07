using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ShipTests
{
    private static readonly Vector2 Start = new(320, 240);

    [Fact]
    public void Ship_starts_at_given_position()
    {
        var ship = new Ship(Start);

        Assert.Equal(Start, ship.Position);
    }

    [Fact]
    public void No_input_does_not_move_ship()
    {
        var ship = new Ship(Start);

        ship.Update(Vector2.Zero, 1f);

        Assert.Equal(Start, ship.Position);
    }

    [Theory]
    [InlineData(-1, 0)] // left
    [InlineData(1, 0)]  // right
    [InlineData(0, -1)] // up
    [InlineData(0, 1)]  // down
    public void Ship_moves_in_input_direction_at_speed(float x, float y)
    {
        var ship = new Ship(Start);

        ship.Update(new Vector2(x, y), 0.5f);

        Assert.Equal(Start + new Vector2(x, y) * Ship.Speed * 0.5f, ship.Position);
    }

    [Theory]
    [InlineData(MovementKey.W, 0, -1)]
    [InlineData(MovementKey.A, -1, 0)]
    [InlineData(MovementKey.S, 0, 1)]
    [InlineData(MovementKey.D, 1, 0)]
    public void Wasd_input_moves_ship_at_speed(MovementKey key, float x, float y)
    {
        var ship = new Ship(Start);
        var direction = ShipInput.KeyToDirection(key);

        ship.Update(direction, 0.5f);

        Assert.Equal(Start + new Vector2(x, y) * Ship.Speed * 0.5f, ship.Position);
    }

    [Fact]
    public void Diagonal_movement_is_not_faster_than_straight()
    {
        var ship = new Ship(Start);

        ship.Update(new Vector2(1, 1), 1f);

        Assert.Equal(Ship.Speed, Vector2.Distance(Start, ship.Position), precision: 3);
    }

    [Fact]
    public void Logo_has_multiple_strokes_instead_of_three_corners()
    {
        var ship = new Ship(Start);

        var strokes = ship.GetStrokes();

        Assert.True(strokes.Count >= 3);
        Assert.All(strokes, stroke => Assert.NotEqual(3, stroke.Length));
    }

    [Fact]
    public void Loops_are_finely_segmented_curves_on_either_side_of_position()
    {
        var ship = new Ship(Start);

        var loops = ship.GetStrokes().OrderByDescending(stroke => stroke.Length).Take(2).ToArray();

        Assert.Equal(2, loops.Length);
        Assert.All(loops, loop =>
        {
            Assert.True(loop.Length >= 33);
            Assert.Equal(loop[0], loop[^1]);
            Assert.True(loop.Min(p => p.Y) < Start.Y);
            Assert.True(loop.Max(p => p.Y) > Start.Y);
            Assert.True(Enumerable.Range(0, loop.Length - 2).Any(i =>
            {
                var first = loop[i + 1] - loop[i];
                var second = loop[i + 2] - loop[i + 1];
                return MathF.Abs(first.X * second.Y - first.Y * second.X) > 0.001f;
            }), "Loop points must curve rather than lie on one straight line");
        });
        Assert.Contains(loops, loop => loop.Min(p => p.X) < Start.X && loop.Max(p => p.X) <= Start.X);
        Assert.Contains(loops, loop => loop.Min(p => p.X) >= Start.X && loop.Max(p => p.X) > Start.X);
    }

    [Fact]
    public void Mid_line_is_a_separate_vertical_stroke_with_distinct_endpoints()
    {
        var ship = new Ship(Start);

        var strokes = ship.GetStrokes();
        var bar = Assert.Single(strokes, stroke => stroke.All(p => MathF.Abs(p.X - Start.X) < 0.001f));

        Assert.True(bar.Length >= 2);
        Assert.True(bar.Min(p => p.Y) < Start.Y);
        Assert.True(bar.Max(p => p.Y) > Start.Y);
        var loops = strokes.Where(stroke => !ReferenceEquals(stroke, bar)).ToArray();
        Assert.Equal(2, loops.Length);
        Assert.All(bar, point => Assert.All(loops, loop =>
            Assert.DoesNotContain(loop, loopPoint => Vector2.Distance(point, loopPoint) < 0.001f)));
    }

    [Fact]
    public void Front_extent_is_strictly_shorter_than_rear_extent()
    {
        var ship = new Ship(Start);

        var points = ship.GetStrokes().SelectMany(stroke => stroke).ToArray();
        var minY = points.Min(p => p.Y) - ship.Position.Y;
        var maxY = points.Max(p => p.Y) - ship.Position.Y;

        Assert.True(minY < 0);
        Assert.True(maxY > 0);
        Assert.True(MathF.Abs(minY) < MathF.Abs(maxY));
    }

    [Fact]
    public void Movement_translates_every_stroke_without_rotating_the_logo()
    {
        var ship = new Ship(Start);
        var original = ship.GetStrokes();
        Vector2[] directions = [Vector2.UnitX, -Vector2.UnitY, new(-1, 1)];

        foreach (var direction in directions)
        {
            var previousPosition = ship.Position;
            ship.Update(direction, 0.5f);
            Assert.NotEqual(previousPosition, ship.Position);
            var moved = ship.GetStrokes();

            Assert.Equal(original.Count, moved.Count);
            for (var stroke = 0; stroke < original.Count; stroke++)
            {
                Assert.Equal(original[stroke].Length, moved[stroke].Length);
                for (var point = 0; point < original[stroke].Length; point++)
                {
                    var expected = original[stroke][point] - Start;
                    var actual = moved[stroke][point] - ship.Position;
                    Assert.True(Vector2.Distance(expected, actual) < 0.001f);
                }
            }
        }
    }
}
