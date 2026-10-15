using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>A projectile builds its shape once per frame, after it moves and advances, and every reader shares it.</summary>
public class ProjectileShapeCacheTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Start = new(300, 300);

    public static TheoryData<string> Kinds => new("laser", "ballista", "bola");

    private static Projectile Create(string kind, Vector2 position, float heading = 0f) => kind switch
    {
        "laser" => new Laser(position, heading),
        "ballista" => new Ballista(position, heading),
        _ => new Bola(position, heading),
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Repeated_reads_in_a_frame_return_the_same_shape_instance(string kind)
    {
        var projectile = Create(kind, Start);
        var beforeUpdate = projectile.GetShape();
        Assert.Same(beforeUpdate, projectile.GetShape());

        projectile.Update(0.016f);
        var first = projectile.GetShape();
        Assert.Same(first, projectile.GetShape());
        Assert.Same(first, projectile.GetShape());
    }

    [Fact]
    public void Bola_shape_after_spin_matches_its_current_balls()
    {
        var bola = new Bola(Start, 0f);
        var initialCenter = bola.GetShape().Circles[0].Center;
        bola.Update(0.05f);
        var shape = bola.GetShape();

        // The spin moved the balls, and the cached shape follows them.
        Assert.NotEqual(initialCenter, shape.Circles[0].Center);
        var line = Assert.Single(shape.Lines);
        Assert.Equal(bola.BallA, line.A);
        Assert.Equal(bola.BallB, line.B);
        Assert.Equal(bola.LineWidth / 2f, line.HalfWidth);
        Assert.Empty(shape.Polygons);
        Assert.Equal([new Circle(bola.BallA, bola.BallRadius), new Circle(bola.BallB, bola.BallRadius)], shape.Circles);
    }

    [Fact]
    public void Ballista_shape_after_move_matches_its_current_shaft_and_head()
    {
        var ballista = new Ballista(Start, MathF.PI / 3f);
        ballista.GetShape();
        ballista.Update(0.1f);
        var shape = ballista.GetShape();

        var shaft = ballista.GetShaft();
        Assert.NotEqual(Start, shaft[0]);
        var line = Assert.Single(shape.Lines);
        Assert.Equal(shaft[0], line.A);
        Assert.Equal(shaft[1], line.B);
        Assert.Equal(ballista.ShaftWidth / 2f, line.HalfWidth);
        Assert.Equal(ballista.GetArrowhead(), Assert.Single(shape.Polygons));
        Assert.Empty(shape.Circles);
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

    [Theory]
    [MemberData(nameof(Kinds))]
    public void IsExpired_matches_the_overlap_of_the_current_shape_frame_by_frame(string kind)
    {
        // Fly right from inside the screen until well past the edge, checking every frame across the boundary.
        var projectile = Create(kind, new Vector2(Width - 60f, Height / 2f), MathF.PI / 2f);
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
    public void Steady_state_update_on_bola_and_ballista_allocates_zero_bytes()
    {
        var bola = new Bola(Start, 0f);
        var ballista = new Ballista(Start, 0f);

        // Warm up
        bola.Update(0.016f);
        _ = bola.GetShape().GetBounds();
        ballista.Update(0.016f);
        _ = ballista.GetShape().GetBounds();

        var beforeBola = GC.GetAllocatedBytesForCurrentThread();
        bola.Update(0.016f);
        _ = bola.GetShape().GetBounds();
        var allocatedBola = GC.GetAllocatedBytesForCurrentThread() - beforeBola;
        Assert.Equal(0L, allocatedBola);

        var beforeBallista = GC.GetAllocatedBytesForCurrentThread();
        ballista.Update(0.016f);
        _ = ballista.GetShape().GetBounds();
        var allocatedBallista = GC.GetAllocatedBytesForCurrentThread() - beforeBallista;
        Assert.Equal(0L, allocatedBallista);
    }

    [Fact]
    public void Shape_after_update_equals_fresh_geometry_by_value()
    {
        var bola = new Bola(Start, 0.5f);
        bola.Update(0.05f);
        var freshBola = Fresh(bola);
        var bolaShape = bola.GetShape();
        Assert.Equal(freshBola.Lines, bolaShape.Lines);
        Assert.Equal(freshBola.Polygons, bolaShape.Polygons);
        Assert.Equal(freshBola.Circles, bolaShape.Circles);

        var ballista = new Ballista(Start, 0.7f);
        ballista.Update(0.1f);
        var freshBallista = Fresh(ballista);
        var ballistaShape = ballista.GetShape();
        Assert.Equal(freshBallista.Lines, ballistaShape.Lines);
        Assert.Equal(freshBallista.Polygons, ballistaShape.Polygons);
        Assert.Equal(freshBallista.Circles, ballistaShape.Circles);
    }

    [Fact]
    public void GetBounds_after_a_move_reflects_the_new_position()
    {
        var ballista = new Ballista(Start, 0f);
        var initialBounds = ballista.GetShape().GetBounds();

        ballista.Update(0.1f);
        var movedBounds = ballista.GetShape().GetBounds();

        Assert.NotEqual(initialBounds, movedBounds);
        Assert.True(movedBounds.Min.Y < initialBounds.Min.Y);
        Assert.True(movedBounds.Max.Y < initialBounds.Max.Y);

        var bola = new Bola(Start, 0f);
        var initialBolaBounds = bola.GetShape().GetBounds();

        bola.Update(0.1f);
        var movedBolaBounds = bola.GetShape().GetBounds();

        Assert.NotEqual(initialBolaBounds, movedBolaBounds);
    }

    /// <summary>The shape built afresh from the projectile's current public geometry, bypassing the cache.</summary>
    private static ProjectileShape Fresh(Projectile projectile) => projectile switch
    {
        Laser laser => new([new ThickLine(laser.GetLine()[0], laser.GetLine()[1], laser.Width / 2f)], [], []),
        Ballista ballista => new(
            [new ThickLine(ballista.GetShaft()[0], ballista.GetShaft()[1], ballista.ShaftWidth / 2f)],
            [ballista.GetArrowhead()], []),
        Bola bola => new(
            [new ThickLine(bola.BallA, bola.BallB, bola.LineWidth / 2f)], [],
            [new Circle(bola.BallA, bola.BallRadius), new Circle(bola.BallB, bola.BallRadius)]),
        _ => throw new ArgumentOutOfRangeException(nameof(projectile)),
    };
}
