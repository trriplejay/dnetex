using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ShipInputTests
{
    [Theory]
    [InlineData(MovementKey.A, -1)]
    [InlineData(MovementKey.D, 1)]
    public void Steering_leaves_position_unchanged(MovementKey key, float sign)
    {
        var start = new Vector2(320, 240);
        var ship = new Ship(start);

        ship.Update(ShipInput.KeyToTurn(key), ShipInput.KeyToThrust(key), 0.25f);

        Assert.Equal(start, ship.Position);
        Assert.Equal(sign * MathF.PI / 4f, ship.Heading, precision: 5);
    }

    [Theory]
    [InlineData(MovementKey.W, 0, 1)]
    [InlineData(MovementKey.A, -1, 0)]
    [InlineData(MovementKey.S, 0, -1)]
    [InlineData(MovementKey.D, 1, 0)]
    public void Wasd_keys_produce_independent_turn_and_thrust(MovementKey key, float turn, float thrust)
    {
        Assert.Equal(turn, ShipInput.KeyToTurn(key));
        Assert.Equal(thrust, ShipInput.KeyToThrust(key));
    }

    [Theory]
    [InlineData(MovementKey.Up)]
    [InlineData(MovementKey.Down)]
    [InlineData(MovementKey.Left)]
    [InlineData(MovementKey.Right)]
    [InlineData(MovementKey.None)]
    [InlineData((MovementKey)999)]
    public void Keys_other_than_wasd_produce_no_turn_or_thrust(MovementKey key)
    {
        Assert.Equal(0f, ShipInput.KeyToTurn(key));
        Assert.Equal(0f, ShipInput.KeyToThrust(key));

        var start = new Vector2(320, 240);
        var ship = new Ship(start);
        ship.Update(ShipInput.KeyToTurn(key), ShipInput.KeyToThrust(key), 1f);

        Assert.Equal(start, ship.Position);
        Assert.Equal(0f, ship.Heading);
    }

    [Fact]
    public void Opposing_keys_cancel_turn_and_thrust()
    {
        var start = new Vector2(320, 240);
        var ship = new Ship(start);
        var turn = ShipInput.KeyToTurn(MovementKey.A) + ShipInput.KeyToTurn(MovementKey.D);
        var thrust = ShipInput.KeyToThrust(MovementKey.W) + ShipInput.KeyToThrust(MovementKey.S);

        ship.Update(turn, thrust, 1f);

        Assert.Equal(start, ship.Position);
        Assert.Equal(0f, ship.Heading);
    }
}
