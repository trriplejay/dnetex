using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>
/// The quick reject ResolveLaserHits runs before the exact hit test: whenever a projectile's bounds can't reach an
/// enemy's bounding circle, the exact test must agree that they don't touch.
/// </summary>
public class ProjectileBoundsTests
{
    [Fact]
    public void Bounds_never_reject_a_pair_the_exact_test_says_touches_over_many_generated_pairs()
    {
        var random = new Random(44);
        var (touching, rejected) = (0, 0);

        for (var i = 0; i < 20_000; i++)
        {
            var projectile = RandomProjectile(random);
            var shape = projectile.GetShape();
            var enemy = RandomEnemyNear(random, projectile.Position);

            var reaches = shape.GetBounds().Reaches(enemy);
            var touches = shape.Touches(enemy.GetStrokes());
            Assert.False(touches && !reaches,
                $"Pair {i}: {projectile.GetType().Name} at {projectile.Position} touches {enemy.Type.Name} at {enemy.Position} (rotation {enemy.Rotation}) but the bounds rejected it");
            touching += touches ? 1 : 0;
            rejected += reaches ? 0 : 1;
        }

        // The generator must actually exercise both sides of the reject.
        Assert.True(touching > 2_000, $"Only {touching} touching pairs");
        Assert.True(rejected > 2_000, $"Only {rejected} rejected pairs");
    }

    [Theory]
    [InlineData(0f, 1.95f)]
    [InlineData(0.7f, 1.95f)]
    [InlineData(2.4f, 3.9f)]
    public void Laser_graze_inside_the_half_width_is_kept_by_the_bounds(float heading, float gap)
    {
        // A tiny enemy beside the laser's middle, `gap` from its centre line: inside the half-width only when the
        // laser is wide enough. Scale 2 makes the half-width 4.
        var scale = gap > 2f ? 2f : 1f;
        var laser = new Laser(new Vector2(300, 300), heading, scale);
        var line = Assert.Single(laser.GetShape().Lines);
        var middle = (line.A + line.B) / 2f;
        var side = Vector2.Normalize(new Vector2(-(line.B - line.A).Y, (line.B - line.A).X));
        var speck = Speck(middle + side * gap);

        Assert.True(laser.GetShape().Touches(speck.GetStrokes()));
        Assert.True(laser.GetShape().GetBounds().Reaches(speck));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.1f)]
    public void Bola_ball_graze_inside_the_radius_is_kept_by_the_bounds(float heading)
    {
        // A tiny enemy 2.9 out from ball B's centre, straight away from the bola: only the ball's radius reaches it.
        var bola = new Bola(new Vector2(300, 300), heading);
        var outward = Vector2.Normalize(bola.BallB - bola.Position);
        var speck = Speck(bola.BallB + outward * 2.9f);

        Assert.True(bola.GetShape().Touches(speck.GetStrokes()));
        Assert.True(bola.GetShape().GetBounds().Reaches(speck));
    }

    [Fact]
    public void Ballista_head_enclosing_an_enemy_is_kept_by_the_bounds()
    {
        var ballista = new Ballista(new Vector2(300, 300), 0f);
        var speck = Speck(new Vector2(300, 300 - 58f));

        Assert.True(ballista.GetShape().Touches(speck.GetStrokes()));
        Assert.True(ballista.GetShape().GetBounds().Reaches(speck));
    }

    [Fact]
    public void Enemy_whose_outline_encloses_the_projectile_is_kept_by_the_bounds()
    {
        var meta = TestEnemies.Stationary(EnemyRoster.GeminiBoss, new Vector2(300, 300));
        var laser = new Laser(new Vector2(300, 300), 0.4f);

        Assert.True(laser.GetShape().Touches(meta.GetStrokes()));
        Assert.True(laser.GetShape().GetBounds().Reaches(meta));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Pairs_far_apart_are_rejected_by_the_bounds(int kind)
    {
        Projectile projectile = kind switch
        {
            0 => new Laser(new Vector2(100, 100), 0.3f, scale: 3f),
            1 => new Bola(new Vector2(100, 100), 0.3f, scale: 3f),
            _ => new Ballista(new Vector2(100, 100), 0.3f, scale: 3f),
        };

        foreach (var type in EnemyRoster.All.Append(EnemyRoster.GeminiBoss))
        {
            var enemy = TestEnemies.Stationary(type, new Vector2(700, 600));
            Assert.False(projectile.GetShape().GetBounds().Reaches(enemy), type.Name);
        }
    }

    [Fact]
    public void Pair_separated_only_within_the_slack_margin_is_kept_by_the_bounds()
    {
        // Laser from (100,100) to (113,100) with half-width 2 has max X = 115.
        // Speck with radius 14 at center (115 + 14 + 0.005, 100) = (129.005, 100).
        // Distance from nearest point on box (115, 100) is 14.005.
        // Without slack (0.01), radius 14 would reject (14.005 > 14). With slack, reach is 14.01, so it reaches.
        var laser = new Laser(new Vector2(100, 100), MathF.PI / 2f);
        var bounds = laser.GetShape().GetBounds();
        var enemy = TestEnemies.Stationary(EnemyRoster.HuggingFace, new Vector2(129.005f, 100f));

        Assert.False(laser.GetShape().Touches(enemy.GetStrokes()));
        Assert.True(bounds.Reaches(enemy));
    }

    [Fact]
    public void Warm_GetBounds_and_Reaches_allocate_zero_bytes()
    {
        var laser = new Laser(new Vector2(300, 300), 0.5f, scale: 2f);
        var bola = new Bola(new Vector2(300, 300), 0.5f, scale: 2f);
        var ballista = new Ballista(new Vector2(300, 300), 0.5f, scale: 2f);
        var enemy = TestEnemies.Stationary(EnemyRoster.Gemini, new Vector2(320, 320));

        // Warm up lazy bounds cache and any static jit
        _ = laser.GetShape().GetBounds().Reaches(enemy);
        _ = bola.GetShape().GetBounds().Reaches(enemy);
        _ = ballista.GetShape().GetBounds().Reaches(enemy);

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
        {
            _ = laser.GetShape().GetBounds().Reaches(enemy);
            _ = bola.GetShape().GetBounds().Reaches(enemy);
            _ = ballista.GetShape().GetBounds().Reaches(enemy);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
    }

    [Fact]
    public void Enemy_bounding_radius_is_its_outlines_furthest_point_at_every_rotation()
    {
        var type = EnemyRoster.ChatGpt;
        var radius = ShapeBounds.OutlineRadius(type.Outline);
        var enemy = TestEnemies.Stationary(type, new Vector2(400, 400));

        for (var frame = 0; frame < 50; frame++)
        {
            var furthest = enemy.GetStrokes().SelectMany(stroke => stroke).Max(p => Vector2.Distance(p, enemy.Position));
            Assert.True(furthest <= radius + 0.001f, $"Rotation {enemy.Rotation}: {furthest} beyond {radius}");
            enemy.Update(Vector2.Zero, 0.137f);
        }
        Assert.True(radius > 0f);
    }

    [Fact]
    public void InvalidateBounds_clears_cached_bounds_and_reflects_in_place_mutation()
    {
        var lines = new ThickLine[] { new(new Vector2(10, 10), new Vector2(20, 10), 1f) };
        var shape = new ProjectileShape(lines, [], []);

        var initialBounds = shape.GetBounds();
        Assert.Equal(new Vector2(9f, 9f), initialBounds.Min);
        Assert.Equal(new Vector2(21f, 11f), initialBounds.Max);

        // Mutate backing array in place
        lines[0] = new ThickLine(new Vector2(100, 100), new Vector2(200, 100), 2f);

        // Before invalidation, cached bounds would still be the initial bounds
        Assert.Equal(initialBounds, shape.GetBounds());

        // Invalidate bounds and verify allocation is 0
        var before = GC.GetAllocatedBytesForCurrentThread();
        shape.InvalidateBounds();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);

        var updatedBounds = shape.GetBounds();
        Assert.Equal(new Vector2(98f, 98f), updatedBounds.Min);
        Assert.Equal(new Vector2(202f, 102f), updatedBounds.Max);
    }

    // A projectile of a random kind, size, heading and age (bolas spin as they age).
    private static Projectile RandomProjectile(Random random)
    {
        var position = new Vector2(200 + random.NextSingle() * 600, 200 + random.NextSingle() * 400);
        var heading = random.NextSingle() * MathF.Tau;
        var scale = 0.5f + random.NextSingle() * 3f;
        var pierce = 0;
        Projectile projectile = random.Next(3) switch
        {
            0 => new Laser(position, heading, scale, pierce),
            1 => new Bola(position, heading, scale, pierce),
            _ => new Ballista(position, heading, scale, pierce),
        };
        projectile.Update(random.NextSingle() * 0.1f);
        return projectile;
    }

    // A roster enemy (or a speck, or the boss, possibly in a later phase) close to the point at a random rotation.
    private static Enemy RandomEnemyNear(Random random, Vector2 near)
    {
        var types = EnemyRoster.All.Append(EnemyRoster.GeminiBoss).Append(SpeckType).ToArray();
        var type = types[random.Next(types.Length)] with { SpinSpeed = 1f, Shield = null };
        var distance = random.NextSingle() * (ShapeBounds.OutlineRadius(type.Outline) + 90f);
        var angle = random.NextSingle() * MathF.Tau;
        var enemy = TestEnemies.Stationary(type, near + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance);
        enemy.Update(Vector2.Zero, random.NextSingle() * MathF.Tau);
        if (type.Phases is not null && random.Next(2) == 0)
            enemy.TakeDamage(type.HitPoints * 0.6f);
        return enemy;
    }

    private static readonly EnemyType SpeckType = EnemyRoster.Gemini with { Outline = [[new(-0.3f, 0), new(0.3f, 0)]] };

    private static Enemy Speck(Vector2 position) => TestEnemies.Stationary(SpeckType, position);
}
