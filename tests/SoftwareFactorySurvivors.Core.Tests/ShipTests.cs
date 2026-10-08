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
    public void New_ship_faces_up_and_thrust_preserves_heading()
    {
        var ship = new Ship(Start);

        Assert.Equal(0f, ship.Heading);
        ship.Thrust(1f, 0.5f);

        Assert.Equal(Start + new Vector2(0, -100), ship.Position);
        Assert.Equal(0f, ship.Heading);
    }

    [Fact]
    public void No_input_preserves_position_and_heading_after_turning()
    {
        var ship = new Ship(Start);
        ship.Turn(1f, 0.25f);
        var heading = ship.Heading;

        ship.Update(0f, 0f, 1f);

        Assert.Equal(Start, ship.Position);
        Assert.Equal(heading, ship.Heading);
    }

    [Fact]
    public void Turning_accumulates_heading_without_moving()
    {
        var ship = new Ship(Start);

        ship.Turn(1f, 0.25f);
        Assert.Equal(MathF.PI / 4f, ship.Heading, precision: 5);
        Assert.Equal(Start, ship.Position);

        ship.Turn(1f, 0.25f);
        Assert.Equal(MathF.PI / 2f, ship.Heading, precision: 5);
        Assert.Equal(Start, ship.Position);

        ship.Turn(-1f, 0.5f);
        Assert.Equal(0f, ship.Heading, precision: 5);
        Assert.Equal(Start, ship.Position);
    }

    [Theory]
    [InlineData(MovementKey.A, MovementKey.W, -1, 0)]
    [InlineData(MovementKey.D, MovementKey.W, 1, 0)]
    [InlineData(MovementKey.A, MovementKey.S, 1, 0)]
    [InlineData(MovementKey.D, MovementKey.S, -1, 0)]
    [InlineData(MovementKey.None, MovementKey.W, 0, -1)]
    [InlineData(MovementKey.None, MovementKey.S, 0, 1)]
    public void Thrust_follows_heading_after_keyboard_turn(
        MovementKey steering, MovementKey thrust, float x, float y)
    {
        var ship = new Ship(Start);
        ApplyKey(ship, steering, 0.5f);
        var heading = ship.Heading;

        ApplyKey(ship, thrust, 0.5f);

        AssertPointNear(Start + new Vector2(x, y) * 100f, ship.Position);
        Assert.Equal(heading, ship.Heading);
    }

    [Theory]
    [InlineData(MovementKey.W, 1)]
    [InlineData(MovementKey.S, -1)]
    public void Thrust_at_oblique_heading_keeps_speed_and_heading(MovementKey key, float sign)
    {
        var ship = new Ship(Start);
        ApplyKey(ship, MovementKey.D, 0.25f);
        var heading = ship.Heading;

        ApplyKey(ship, key, 0.5f);

        var component = sign * 100f / MathF.Sqrt(2f);
        AssertPointNear(Start + new Vector2(component, -component), ship.Position);
        Assert.Equal(100f, Vector2.Distance(Start, ship.Position), precision: 3);
        Assert.Equal(heading, ship.Heading);
    }

    [Fact]
    public void Simultaneous_turn_and_thrust_uses_new_heading()
    {
        var ship = new Ship(Start);

        ship.Update(ShipInput.KeyToTurn(MovementKey.D), ShipInput.KeyToThrust(MovementKey.W), 0.5f);

        Assert.Equal(MathF.PI / 2f, ship.Heading, precision: 5);
        AssertPointNear(Start + new Vector2(100, 0), ship.Position);
    }

    [Fact]
    public void Zero_elapsed_time_preserves_position_and_heading()
    {
        var ship = new Ship(Start);
        ship.Turn(-1f, 0.25f);
        var heading = ship.Heading;

        ship.Update(1f, 1f, 0f);

        Assert.Equal(Start, ship.Position);
        Assert.Equal(heading, ship.Heading);
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
    public void Front_extent_equals_rear_extent()
    {
        var ship = new Ship(Start);

        var points = ship.GetStrokes().SelectMany(stroke => stroke).ToArray();
        var minY = points.Min(p => p.Y) - ship.Position.Y;
        var maxY = points.Max(p => p.Y) - ship.Position.Y;

        Assert.True(minY < 0);
        Assert.True(maxY > 0);
        Assert.Equal(MathF.Abs(maxY), MathF.Abs(minY), precision: 3);
    }

    [Fact]
    public void Right_turn_rotates_every_stroke_about_position()
    {
        var ship = new Ship(Start);
        var original = ship.GetStrokes();

        ApplyKey(ship, MovementKey.D, 0.5f);
        Assert.Equal(Start, ship.Position);
        Assert.Equal(MathF.PI / 2f, ship.Heading, precision: 5);

        var turned = ship.GetStrokes();
        Assert.Equal(original.Count, turned.Count);
        for (var stroke = 0; stroke < original.Count; stroke++)
        {
            Assert.Equal(original[stroke].Length, turned[stroke].Length);
            for (var point = 0; point < original[stroke].Length; point++)
            {
                var local = original[stroke][point] - Start;
                var expected = ship.Position + new Vector2(-local.Y, local.X);
                Assert.True(Vector2.Distance(expected, turned[stroke][point]) < 0.001f);
            }
        }
        Assert.NotEqual(original[2][0], turned[2][0]);
    }

    [Fact]
    public void Default_heading_emits_fixed_up_logo_points()
    {
        var ship = new Ship(Start);

        var strokes = ship.GetStrokes();

        Assert.Equal(3, strokes.Count);
        for (var loop = 0; loop < 2; loop++)
        {
            Assert.Equal(49, strokes[loop].Length);
            var centerX = loop == 0 ? -8f : 8f;
            for (var point = 0; point < 48; point++)
            {
                var angle = Math.Tau * point / 48;
                var expected = Start + new Vector2(
                    centerX + (float)(8 * Math.Cos(angle)), (float)(6 * Math.Sin(angle)));
                AssertPointNear(expected, strokes[loop][point]);
            }
            Assert.Equal(strokes[loop][0], strokes[loop][48]);
        }
        Assert.Equal(new[] { Start + new Vector2(0, -16), Start + new Vector2(0, 16) }, strokes[2]);
    }

    [Theory]
    [InlineData(-1, 0.25f)]
    [InlineData(1, 0.123f)]
    [InlineData(1, 1.25f)]
    public void Every_stroke_rotates_by_heading_about_position(float turn, float seconds)
    {
        var ship = new Ship(Start);
        var original = ship.GetStrokes();

        ship.Turn(turn, seconds);

        Assert.Equal(Start, ship.Position);
        AssertRotatedStrokes(original, ship, ship.Heading);
        Assert.NotEqual(original[2][0], ship.GetStrokes()[2][0]);
    }

    [Fact]
    public void Left_then_forward_wires_input_to_heading_position_and_drawn_logo()
    {
        var ship = new Ship(Start);
        var original = ship.GetStrokes();

        ApplyKey(ship, MovementKey.A, 0.25f);
        Assert.Equal(-MathF.PI / 4f, ship.Heading, precision: 5);
        Assert.Equal(Start, ship.Position);

        ApplyKey(ship, MovementKey.W, 0.5f);

        var component = 100f / MathF.Sqrt(2f);
        AssertPointNear(Start + new Vector2(-component, -component), ship.Position);
        Assert.True(ship.Position.X < Start.X);
        Assert.Equal(-MathF.PI / 4f, ship.Heading, precision: 5);
        AssertRotatedStrokes(original, ship, -MathF.PI / 4f);
    }

    [Theory]
    [InlineData(0f, 0f, -1f)]
    [InlineData(0.5f, 1f, 0f)]
    [InlineData(-0.5f, -1f, 0f)]
    [InlineData(1f, 0f, 1f)]
    public void Nose_and_forward_follow_heading_and_position(float turnSeconds, float x, float y)
    {
        var ship = new Ship(Start);
        ship.Turn(1f, turnSeconds);
        ship.Thrust(1f, 0.25f);

        var expectedForward = new Vector2(x, y);
        AssertPointNear(expectedForward, ship.Forward);
        Assert.Equal(1f, ship.Forward.Length(), precision: 5);
        AssertPointNear(Start + expectedForward * 66f, ship.Nose);
        AssertPointNear(ship.GetStrokes()[2][0], ship.Nose);
    }

    [Fact]
    public void Nose_and_forward_follow_an_oblique_heading()
    {
        var ship = new Ship(Start);
        ship.Turn(1f, 0.25f);
        var component = 1f / MathF.Sqrt(2f);

        AssertPointNear(new Vector2(component, -component), ship.Forward);
        AssertPointNear(Start + new Vector2(16f * component, -16f * component), ship.Nose);
        Assert.Equal(ship.GetStrokes()[2][0], ship.Nose);
    }

    private static void ApplyKey(Ship ship, MovementKey key, float seconds) =>
        ship.Update(ShipInput.KeyToTurn(key), ShipInput.KeyToThrust(key), seconds);

    private static void AssertRotatedStrokes(IReadOnlyList<Vector2[]> original, Ship ship, float angle)
    {
        var rotated = ship.GetStrokes();
        Assert.Equal(original.Count, rotated.Count);
        for (var stroke = 0; stroke < original.Count; stroke++)
        {
            Assert.Equal(original[stroke].Length, rotated[stroke].Length);
            for (var point = 0; point < original[stroke].Length; point++)
            {
                var local = original[stroke][point] - Start;
                var expected = ship.Position + new Vector2(
                    (float)(local.X * Math.Cos(angle) - local.Y * Math.Sin(angle)),
                    (float)(local.X * Math.Sin(angle) + local.Y * Math.Cos(angle)));
                AssertPointNear(expected, rotated[stroke][point]);
            }
        }
    }

    private static void AssertPointNear(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 0.001f, $"Expected {expected}, actual {actual}");
}
