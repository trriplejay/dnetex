using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>A projectile builds its shape once per frame, after it moves and advances, and every reader shares it.</summary>
public class ProjectileShapeCacheTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Start = new(300, 300);

    [Fact]
    public void Repeated_reads_in_a_frame_return_the_same_shape_instance()
    {
        var projectile = new Laser(Start, 0f);
        var beforeUpdate = projectile.GetShape();
        Assert.Same(beforeUpdate, projectile.GetShape());

        projectile.Update(0.016f);
        var first = projectile.GetShape();
        Assert.Same(first, projectile.GetShape());
        Assert.Same(first, projectile.GetShape());
    }

    [Fact]
    public void Laser_shape_after_move_matches_its_current_line()
    {
        var laser = new Laser(Start, 1f);
        laser.GetShape();
        laser.Update(0.1f);
        var line = Assert.Single(laser.GetShape().Lines);
        var expected = laser.GetLine();
        Assert.Equal(expected[0], line.A);
        Assert.Equal(expected[1], line.B);
        Assert.Equal(laser.Width / 2f, line.HalfWidth);
    }

    [Fact]
    public void IsExpired_matches_the_overlap_of_the_current_shape_frame_by_frame()
    {
        // Fly right from inside the screen until well past the edge, checking every frame across the boundary.
        var projectile = new Laser(new Vector2(Width - 60f, Height / 2f), MathF.PI / 2f);
        var sawLive = false;
        var sawExpired = false;
        for (var frame = 0; frame < 60; frame++)
        {
            projectile.Update(1f / 60f);
            var expired = projectile.IsExpired(Width, Height);
            Assert.Equal(!Fresh(projectile).Overlaps(Width, Height), expired);
            sawLive |= !expired;
            sawExpired |= expired;
        }

        Assert.True(sawLive);
        Assert.True(sawExpired);
    }

    [Fact]
    public void GetBounds_after_a_move_reflects_the_new_position()
    {
        var laser = new Laser(Start, 0f);
        var initialBounds = laser.GetShape().GetBounds();

        laser.Update(0.1f);
        var movedBounds = laser.GetShape().GetBounds();

        Assert.NotEqual(initialBounds, movedBounds);
        Assert.True(movedBounds.Min.Y < initialBounds.Min.Y);
        Assert.True(movedBounds.Max.Y < initialBounds.Max.Y);
    }

    /// <summary>The shape built afresh from the projectile's current public geometry, bypassing the cache.</summary>
    private static ProjectileShape Fresh(Projectile projectile) => projectile switch
    {
        Laser laser => new([new ThickLine(laser.GetLine()[0], laser.GetLine()[1], laser.Width / 2f)], [], []),
        _ => throw new ArgumentOutOfRangeException(nameof(projectile)),
    };
}
