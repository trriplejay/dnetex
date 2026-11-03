using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>Circle hit tests, used by round projectile and hazard shapes.</summary>
public class WeaponPathTests
{
    [Fact]
    public void Circle_touches_strokes_within_its_radius_or_around_its_centre()
    {
        Vector2[][] segment = [[new(5, -10), new(5, 10)]];
        Vector2[][] box = [[new(-20, -20), new(20, -20), new(20, 20), new(-20, 20), new(-20, -20)]];

        Assert.True(Collision.CircleTouches(Vector2.Zero, 5f, segment));
        Assert.False(Collision.CircleTouches(Vector2.Zero, 4.9f, segment));
        Assert.True(Collision.CircleTouches(Vector2.Zero, 1f, box));
    }
}
