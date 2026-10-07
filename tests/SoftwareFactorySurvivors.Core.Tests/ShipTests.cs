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

        ship.Update(0f, 0f, 1f);

        Assert.Equal(Start, ship.Position);
    }

    [Fact]
    public void Default_heading_renders_tip_up()
    {
        var ship = new Ship(Start);

        var vertices = ship.GetVertices();

        Assert.Equal(0f, ship.Heading);
        Assert.Equal(3, vertices.Length);
        Assert.Equal(vertices.Min(p => p.Y), vertices[0].Y);
        Assert.True(vertices[0].Y < ship.Position.Y, "nose should point up");
        AssertVector(Start + new Vector2(0, -12), vertices[0]);
        AssertVector(Start + new Vector2(9, 9), vertices[1]);
        AssertVector(Start + new Vector2(-9, 9), vertices[2]);
    }

    [Fact]
    public void Vertices_rotate_a_quarter_turn_about_position()
    {
        var ship = new Ship(Start);
        ship.Update(0f, 1f, 0.25f);

        var vertices = ship.GetVertices();

        Assert.Equal(MathF.PI / 2f, ship.Heading, precision: 5);
        Assert.Equal(3, vertices.Length);
        AssertVector(Start + new Vector2(12, 0), vertices[0]);
        AssertVector(Start + new Vector2(-9, 9), vertices[1]);
        AssertVector(Start + new Vector2(-9, -9), vertices[2]);
    }

    [Theory]
    [InlineData(0f, true, 0f, -100f)]
    [InlineData(0f, false, 0f, 100f)]
    [InlineData(0.25f, true, 100f, 0f)]
    [InlineData(0.25f, false, -100f, 0f)]
    [InlineData(-0.25f, true, -100f, 0f)]
    [InlineData(0.125f, true, 70.71068f, -70.71068f)]
    [InlineData(0.125f, false, -70.71068f, 70.71068f)]
    public void Thrust_follows_heading_in_both_directions(
        float turns, bool forward, float expectedX, float expectedY)
    {
        var ship = new Ship(Start);
        ship.Update(0f, MathF.Sign(turns), MathF.Abs(turns));
        var heading = ship.Heading;
        var input = ShipInput.FromKeys(forward, !forward, false, false);

        ship.Update(input.Thrust, input.Rotation, 0.5f);

        AssertVector(Start + new Vector2(expectedX, expectedY), ship.Position);
        Assert.Equal(heading, ship.Heading);
    }

    [Theory]
    [InlineData(1f, 0.125f)]
    [InlineData(-1f, 0.5f)]
    [InlineData(1f, 1f)]
    public void Thrust_preserves_200_pixels_per_second(float thrust, float elapsed)
    {
        var ship = new Ship(Start);
        ship.Update(0f, 1f, 0.125f);

        ship.Update(thrust, 0f, elapsed);

        Assert.Equal(200f, Ship.Speed);
        Assert.Equal(200f * elapsed, Vector2.Distance(Start, ship.Position), precision: 3);
    }

    [Theory]
    [InlineData(true, -1f)]
    [InlineData(false, 1f)]
    public void Rotation_keys_turn_visually_without_translation(bool left, float sign)
    {
        var ship = new Ship(Start);
        var input = ShipInput.FromKeys(false, false, left, !left);

        ship.Update(input.Thrust, input.Rotation, 0.25f);

        Assert.Equal(sign * MathF.PI / 2f, ship.Heading, precision: 5);
        Assert.Equal(Start, ship.Position);
        AssertVector(Start + new Vector2(sign * 12f, 0f), ship.GetVertices()[0]);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(1f)]
    public void Rotation_advances_each_active_frame_and_stops_on_release(float rotation)
    {
        var ship = new Ship(Start);
        for (var frame = 1; frame <= 3; frame++)
        {
            ship.Update(0f, rotation, 0.125f);
            Assert.Equal(rotation * frame * MathF.PI / 4f, ship.Heading, precision: 5);
        }
        var heading = ship.Heading;

        ship.Update(0f, 0f, 0.5f);

        Assert.Equal(heading, ship.Heading);
        Assert.Equal(Start, ship.Position);
    }

    [Theory]
    [InlineData(1, -1f)]
    [InlineData(1, 1f)]
    [InlineData(60, -1f)]
    [InlineData(60, 1f)]
    public void One_second_accumulates_a_full_turn_regardless_of_frame_count(int frames, float rotation)
    {
        var ship = new Ship(Start);

        for (var frame = 0; frame < frames; frame++)
            ship.Update(0f, rotation, 1f / frames);

        Assert.Equal(2f * MathF.PI, Ship.RotationSpeed, precision: 5);
        Assert.Equal(rotation * 2f * MathF.PI, ship.Heading, precision: 5);
    }

    [Fact]
    public void Simultaneous_rotation_and_thrust_use_new_heading_and_curve_path()
    {
        var ship = new Ship(Start);
        var input = ShipInput.FromKeys(true, false, false, true);
        var displacements = new Vector2[3];

        for (var frame = 0; frame < displacements.Length; frame++)
        {
            var before = ship.Position;
            ship.Update(input.Thrust, input.Rotation, 0.125f);
            displacements[frame] = ship.Position - before;
        }

        Assert.Equal(3f * MathF.PI / 4f, ship.Heading, precision: 5);
        AssertVector(new Vector2(17.67767f, -17.67767f), displacements[0]);
        AssertVector(new Vector2(25f, 0f), displacements[1]);
        AssertVector(new Vector2(17.67767f, 17.67767f), displacements[2]);
        for (var frame = 1; frame < displacements.Length; frame++)
        {
            var previous = displacements[frame - 1];
            var current = displacements[frame];
            Assert.True(MathF.Abs(previous.X * current.Y - previous.Y * current.X) > 1f,
                "Successive displacements should not be parallel");
        }
    }

    [Theory]
    [InlineData(false, false, false, false, 0f, 0f)]
    [InlineData(false, false, false, true, 0f, 1f)]
    [InlineData(false, false, true, false, 0f, -1f)]
    [InlineData(false, false, true, true, 0f, 0f)]
    [InlineData(false, true, false, false, -1f, 0f)]
    [InlineData(false, true, false, true, -1f, 1f)]
    [InlineData(false, true, true, false, -1f, -1f)]
    [InlineData(false, true, true, true, -1f, 0f)]
    [InlineData(true, false, false, false, 1f, 0f)]
    [InlineData(true, false, false, true, 1f, 1f)]
    [InlineData(true, false, true, false, 1f, -1f)]
    [InlineData(true, false, true, true, 1f, 0f)]
    [InlineData(true, true, false, false, 0f, 0f)]
    [InlineData(true, true, false, true, 0f, 1f)]
    [InlineData(true, true, true, false, 0f, -1f)]
    [InlineData(true, true, true, true, 0f, 0f)]
    public void Held_keys_map_to_independent_intents(
        bool forward, bool backward, bool left, bool right, float thrust, float rotation)
    {
        var input = ShipInput.FromKeys(forward, backward, left, right);

        Assert.Equal(thrust, input.Thrust);
        Assert.Equal(rotation, input.Rotation);
    }

    private static void AssertVector(Vector2 expected, Vector2 actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 3);
        Assert.Equal(expected.Y, actual.Y, precision: 3);
    }
}
