using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ShipInputTests
{
    [Theory]
    [InlineData(MovementKey.W, 0, -1)]
    [InlineData(MovementKey.A, -1, 0)]
    [InlineData(MovementKey.S, 0, 1)]
    [InlineData(MovementKey.D, 1, 0)]
    public void Wasd_keys_produce_cardinal_directions(MovementKey key, float x, float y)
    {
        Assert.Equal(new Vector2(x, y), ShipInput.KeyToDirection(key));
    }

    [Theory]
    [InlineData(MovementKey.Up)]
    [InlineData(MovementKey.Down)]
    [InlineData(MovementKey.Left)]
    [InlineData(MovementKey.Right)]
    public void Arrow_keys_produce_no_direction(MovementKey key)
    {
        Assert.Equal(Vector2.Zero, ShipInput.KeyToDirection(key));
    }

    [Theory]
    [InlineData(MovementKey.Up)]
    [InlineData(MovementKey.Down)]
    [InlineData(MovementKey.Left)]
    [InlineData(MovementKey.Right)]
    [InlineData(MovementKey.None)]
    [InlineData((MovementKey)999)]
    public void Keys_other_than_wasd_produce_no_direction(MovementKey key)
    {
        Assert.Equal(Vector2.Zero, ShipInput.KeyToDirection(key));
    }
}
