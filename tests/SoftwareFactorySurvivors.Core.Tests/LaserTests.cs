using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class LaserTests
{
    private static readonly Vector2 Start = new(320, 240);

    [Fact]
    public void Projectile_advances_by_speed_and_elapsed_seconds()
    {
        Projectile projectile = new Laser(Start, 0f);

        projectile.Update(0.25f);

        Assert.Equal(3 * Ship.Speed, projectile.Speed);
        AssertPointNear(Start + new Vector2(0, -150), projectile.Position);
    }

    [Theory]
    [InlineData(0f, 0f, -1f)]
    [InlineData(0.5f, 1f, 0f)]
    [InlineData(1f, 0f, 1f)]
    [InlineData(-0.5f, -1f, 0f)]
    public void Laser_moves_at_three_times_ship_speed(float halfTurns, float x, float y)
    {
        var laser = new Laser(Start, halfTurns * MathF.PI);

        laser.Update(1f);

        AssertPointNear(Start + new Vector2(x, y) * (3 * Ship.Speed), laser.Position);
        Assert.Equal(3 * Ship.Speed, Vector2.Distance(Start, laser.Position), precision: 3);
    }

    [Fact]
    public void Successive_positions_stay_on_the_original_oblique_ray()
    {
        var heading = MathF.PI / 4f;
        var laser = new Laser(Start, heading);
        var elapsed = 0f;

        foreach (var step in new[] { 0f, 0.125f, 0.25f, 0.5f })
        {
            elapsed += step;
            laser.Update(step);

            var component = 3 * Ship.Speed * elapsed / MathF.Sqrt(2f);
            AssertPointNear(Start + new Vector2(component, -component), laser.Position);
            Assert.Equal(heading, laser.Heading);
        }
    }

    [Fact]
    public void Turning_ship_after_firing_does_not_bend_laser()
    {
        var ship = new Ship(Start);
        var timer = new FiringTimer();
        var shot = Assert.Single(timer.Update(1f, ship.Nose, ship.Heading));
        var laser = new Laser(shot.Position, shot.Heading);
        var spawn = laser.Position;

        ship.AimAt(ship.Position + new Vector2(100, 0));
        laser.Update(0.5f);

        Assert.Equal(0f, laser.Heading);
        Assert.NotEqual(ship.Heading, laser.Heading);
        AssertPointNear(spawn + new Vector2(0, -300), laser.Position);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void Drawable_is_a_thirteen_pixel_segment_from_current_position(float elapsed)
    {
        var laser = new Laser(Start, MathF.PI / 4f);
        laser.Update(elapsed);

        var line = laser.GetLine();

        Assert.Equal(2, line.Length);
        AssertPointNear(laser.Position, line[0]);
        Assert.Equal(13f, Vector2.Distance(line[0], line[1]), precision: 3);
        var component = 13f / MathF.Sqrt(2f);
        AssertPointNear(line[0] + new Vector2(component, -component), line[1]);
    }

    [Fact]
    public void Laser_expires_after_travelling_fully_offscreen()
    {
        var laser = new Laser(new Vector2(50, 50), 0f);
        Assert.False(laser.IsExpired(100, 100));

        laser.Update(0.25f);

        Assert.True(laser.IsExpired(100, 100));
    }

    [Theory]
    [InlineData(50, 0, false)]
    [InlineData(50, 113, false)]
    [InlineData(50, 113.01f, true)]
    [InlineData(50, 105, false)]
    [InlineData(50, -0.01f, true)]
    [InlineData(0, 50, false)]
    [InlineData(100, 50, false)]
    [InlineData(-0.01f, 50, true)]
    [InlineData(100.01f, 50, true)]
    public void Expiry_includes_the_whole_segment_and_closed_boundaries(float x, float y, bool expired)
    {
        var laser = new Laser(new Vector2(x, y), 0f);

        Assert.Equal(expired, laser.IsExpired(100, 100));
    }

    [Fact]
    public void Crossing_segment_is_visible_even_when_both_endpoints_are_outside()
    {
        var laser = new Laser(new Vector2(-2, 5), MathF.PI / 4f);

        Assert.False(laser.IsExpired(100, 100));
    }

    [Fact]
    public void Segment_missing_corner_expires_even_when_its_bounding_box_overlaps()
    {
        var laser = new Laser(new Vector2(-5, 2), MathF.PI / 4f);

        Assert.True(laser.IsExpired(100, 100));
    }

    [Fact]
    public void Expiry_uses_supplied_width_and_height()
    {
        var laser = new Laser(new Vector2(150, 150), 0f);

        Assert.True(laser.IsExpired(100, 200));
        Assert.True(laser.IsExpired(200, 100));
        Assert.False(laser.IsExpired(200, 200));
    }

    [Fact]
    public void Laser_carries_per_shot_damage()
    {
        var defaultLaser = new Laser(Start, 0f);
        Assert.Equal(1f, defaultLaser.Damage);

        var customLaser = new Laser(Start, 0f, scale: 2f, pierce: 1, damage: 3.5f);
        Assert.Equal(3.5f, customLaser.Damage);
    }

    [Fact]
    public void Level_5_explosion_hits_other_live_enemies_within_forty_pixels_and_leaves_beyond_untouched()
    {
        // Struck enemy at impact point (0, 0)
        var impactPoint = new Vector2(100, 100);
        var struckEnemy = TestEnemies.StationaryGemini(impactPoint);
        // Struck enemy takes primary damage first
        struckEnemy.TakeDamage(10f);

        // Enemy at 0 px from impact (another enemy overlapping impact position)
        var enemyAt0 = TestEnemies.StationaryGemini(impactPoint);
        // Enemy at 30 px
        var enemyAt30 = TestEnemies.StationaryGemini(impactPoint + new Vector2(30, 0));
        // Enemy at 60 px
        var enemyAt60 = TestEnemies.StationaryGemini(impactPoint + new Vector2(60, 0));

        var allEnemies = new[] { struckEnemy, enemyAt0, enemyAt30, enemyAt60 };

        // Test level 5 explosion
        var targets = Laser.GetExplosionTargets(impactPoint, allEnemies, weaponLevel: 5, struckEnemy: struckEnemy);
        Assert.Contains(enemyAt0, targets);
        Assert.Contains(enemyAt30, targets);
        Assert.DoesNotContain(enemyAt60, targets);
        Assert.DoesNotContain(struckEnemy, targets);

        var shotDamage = 2.5f;
        var damaged = Laser.ApplyExplosion(impactPoint, shotDamage, weaponLevel: 5, allEnemies, struckEnemy: struckEnemy);
        Assert.Equal(2, damaged.Count);

        // enemyAt0 and enemyAt30 took 2.5 damage (Gemini has 3 HP -> 0.5 HP)
        Assert.Equal(EnemyRoster.Gemini.HitPoints - shotDamage, enemyAt0.HitPoints);
        Assert.Equal(EnemyRoster.Gemini.HitPoints - shotDamage, enemyAt30.HitPoints);
        // enemyAt60 took no damage (still at full HP)
        Assert.Equal(EnemyRoster.Gemini.HitPoints, enemyAt60.HitPoints);
    }

    [Fact]
    public void Below_level_5_no_explosion_targets_or_damage_occur()
    {
        var impactPoint = new Vector2(100, 100);
        var struckEnemy = TestEnemies.StationaryGemini(impactPoint);
        var enemyAt0 = TestEnemies.StationaryGemini(impactPoint);
        var enemyAt30 = TestEnemies.StationaryGemini(impactPoint + new Vector2(30, 0));
        var enemyAt60 = TestEnemies.StationaryGemini(impactPoint + new Vector2(60, 0));

        var allEnemies = new[] { struckEnemy, enemyAt0, enemyAt30, enemyAt60 };

        for (var level = 1; level <= 4; level++)
        {
            var targets = Laser.GetExplosionTargets(impactPoint, allEnemies, weaponLevel: level, struckEnemy: struckEnemy);
            Assert.Empty(targets);

            var damaged = Laser.ApplyExplosion(impactPoint, damage: 5f, weaponLevel: level, allEnemies, struckEnemy: struckEnemy);
            Assert.Empty(damaged);

            Assert.Equal(EnemyRoster.Gemini.HitPoints, enemyAt0.HitPoints);
            Assert.Equal(EnemyRoster.Gemini.HitPoints, enemyAt30.HitPoints);
            Assert.Equal(EnemyRoster.Gemini.HitPoints, enemyAt60.HitPoints);
        }
    }

    [Fact]
    public void Explosion_ignores_dead_enemies_within_radius()
    {
        var impactPoint = new Vector2(100, 100);
        var deadEnemy = TestEnemies.StationaryGemini(impactPoint + new Vector2(10, 0));
        deadEnemy.TakeDamage(100f);
        Assert.True(deadEnemy.IsDead);

        var liveEnemy = TestEnemies.StationaryGemini(impactPoint + new Vector2(20, 0));

        var targets = Laser.GetExplosionTargets(impactPoint, new[] { deadEnemy, liveEnemy }, weaponLevel: 5);
        Assert.Single(targets);
        Assert.Contains(liveEnemy, targets);
        Assert.DoesNotContain(deadEnemy, targets);
    }

    private static void AssertPointNear(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 0.001f, $"Expected {expected}, actual {actual}");
}
