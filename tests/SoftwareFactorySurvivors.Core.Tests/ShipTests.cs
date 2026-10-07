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

    [Fact]
    public void Diagonal_movement_is_not_faster_than_straight()
    {
        var ship = new Ship(Start);

        ship.Update(new Vector2(1, 1), 1f);

        Assert.Equal(Ship.Speed, Vector2.Distance(Start, ship.Position), precision: 3);
    }

    [Fact]
    public void Vertices_form_a_triangle_around_position()
    {
        var ship = new Ship(Start);

        var vertices = ship.GetVertices();

        Assert.Equal(3, vertices.Length);
        Assert.True(vertices[0].Y < Start.Y, "nose should point up");
    }
}
