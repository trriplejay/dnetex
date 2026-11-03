using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class OrbitingOrbsTests
{
    private static readonly Vector2 Ship = new(500f, 400f);
    private const float Eps = 0.01f;

    private static readonly IReadOnlyList<Enemy> NoEnemies = [];

    private static Enemy Gemini(Vector2 offset) => TestEnemies.StationaryGemini(Ship + offset);

    /// <summary>A 200 x 200 square, big enough that an orb stays inside it for more than a second.</summary>
    private static Enemy BigSquare(Vector2 center, float half = 100f)
    {
        Vector2[] square = [new(-half, -half), new(half, -half), new(half, half), new(-half, half), new(-half, -half)];
        var type = new EnemyType("Big", half, 1000f, 0f, 0, 0f, 0f, 0f, speed => new ChasePlayer(speed), [square]);
        return new Enemy(type, center);
    }

    [Fact]
    public void Level_1_is_three_orbs_120_degrees_apart_at_80_px_with_radius_8()
    {
        var orbs = new OrbitingOrbs();

        var positions = orbs.GetOrbPositions(Ship);

        Assert.Equal(3, positions.Count);
        Assert.Equal(8f, orbs.OrbRadius);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(80f, Vector2.Distance(Ship, positions[i]), Eps);
            var angle = MathF.Atan2(positions[i].Y - Ship.Y, positions[i].X - Ship.X);
            var expected = MathF.Tau * i / 3f;
            Assert.Equal(MathF.Cos(expected), MathF.Cos(angle), 1e-3f);
            Assert.Equal(MathF.Sin(expected), MathF.Sin(angle), 1e-3f);
        }
    }

    [Fact]
    public void After_one_second_the_orbs_have_turned_90_degrees_clockwise_and_follow_the_ship()
    {
        var orbs = new OrbitingOrbs();

        orbs.Update(1f, Ship, NoEnemies);

        // Clockwise with y pointing down: the orb that started on the right is now below the ship.
        var first = orbs.GetOrbPositions(Ship)[0];
        Assert.Equal(Ship.X, first.X, Eps);
        Assert.Equal(Ship.Y + 80f, first.Y, Eps);

        var moved = new Vector2(100f, 50f);
        var followed = orbs.GetOrbPositions(Ship + moved)[0];
        Assert.Equal(first.X + moved.X, followed.X, Eps);
        Assert.Equal(first.Y + moved.Y, followed.Y, Eps);
    }

    [Fact]
    public void An_enemy_parked_in_the_orbs_path_takes_damage_at_most_once_per_half_second()
    {
        var orbs = new OrbitingOrbs();
        var enemy = BigSquare(Ship + new Vector2(80f, 0f));
        var hits = new List<(Enemy Enemy, float Damage)>();

        for (var i = 0; i < 8; i++)
            hits.AddRange(orbs.Update(0.125f, Ship, [enemy]));

        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit =>
        {
            Assert.Same(enemy, hit.Enemy);
            Assert.Equal(1f, hit.Damage);
        });
    }

    [Fact]
    public void An_enemy_off_the_orbit_is_never_hit_and_a_dead_one_is_skipped()
    {
        var orbs = new OrbitingOrbs();
        var centre = Gemini(Vector2.Zero);
        var far = Gemini(new Vector2(300f, 0f));
        var dead = Gemini(new Vector2(80f, 0f));
        dead.TakeDamage(100f);

        var hits = orbs.Update(0.05f, Ship, [centre, far, dead]);

        Assert.Empty(hits);
    }

    [Fact]
    public void The_four_options_change_exactly_their_own_stats()
    {
        var orbs = new OrbitingOrbs();

        orbs.AddOrb();
        Assert.Equal((4, 90f, 80f, 8f, 1f), Stats(orbs));
        orbs.AddSpeedAndOrbit();
        Assert.Equal((4, 120f, 95f, 8f, 1f), Stats(orbs));
        orbs.AddOrbRadius();
        Assert.Equal((4, 120f, 95f, 11f, 1f), Stats(orbs));
        orbs.AddDamage();
        Assert.Equal((4, 120f, 95f, 11f, 1.5f), Stats(orbs));
        Assert.Equal(4, orbs.GetOrbPositions(Ship).Count);
    }

    [Fact]
    public void A_bigger_orb_radius_reaches_enemies_a_smaller_one_misses()
    {
        // A 10 px square centred 100 px out has its nearest edge at 90 px: past radius 8's reach (88), within radius 17's (97).
        var enemy = BigSquare(Ship + new Vector2(100f, 0f), half: 10f);
        Assert.Empty(new OrbitingOrbs().Update(0f, Ship, [enemy]));

        var bigger = new OrbitingOrbs();
        bigger.AddOrbRadius();
        bigger.AddOrbRadius();
        bigger.AddOrbRadius();
        Assert.Single(bigger.Update(0f, Ship, [enemy]));
    }

    [Fact]
    public void Update_rejects_a_negative_or_non_finite_time()
    {
        var orbs = new OrbitingOrbs();

        Assert.Throws<ArgumentOutOfRangeException>(() => orbs.Update(-0.1f, Ship, NoEnemies));
        Assert.Throws<ArgumentOutOfRangeException>(() => orbs.Update(float.NaN, Ship, NoEnemies));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Levels_outside_1_to_20_are_rejected(int level)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrbitingOrbs(level));
        var orbs = new OrbitingOrbs(1);
        Assert.Throws<ArgumentOutOfRangeException>(() => orbs.SetLevel(level));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(20)]
    public void Levels_1_to_20_are_accepted(int level)
    {
        var orbs = new OrbitingOrbs(level);
        Assert.Equal(level, orbs.Level);

        var existing = new OrbitingOrbs(1);
        existing.SetLevel(level);
        Assert.Equal(level, existing.Level);
    }

    [Fact]
    public void Level_4_still_has_discrete_orbs_and_level_5_has_none()
    {
        Assert.False(new OrbitingOrbs(4).IsRingMode);
        Assert.Equal(3, new OrbitingOrbs(4).GetOrbPositions(Ship).Count);

        var ring = new OrbitingOrbs(5);
        Assert.True(ring.IsRingMode);
        Assert.Empty(ring.GetOrbPositions(Ship));
    }

    [Fact]
    public void An_enemy_on_the_ring_band_for_2_and_a_half_seconds_takes_two_hits()
    {
        var ring = new OrbitingOrbs(5);
        var enemy = Gemini(new Vector2(80f, 0f));
        var hits = new List<(Enemy Enemy, float Damage)>();

        for (var i = 0; i < 5; i++)
            hits.AddRange(ring.Update(0.5f, Ship, [enemy]));

        Assert.Equal(2, hits.Count);
        Assert.All(hits, hit => Assert.Equal(1f, hit.Damage));
    }

    [Fact]
    public void The_ring_hit_is_the_orb_damage_after_upgrades()
    {
        var ring = new OrbitingOrbs(5);
        ring.AddDamage();
        var enemy = Gemini(new Vector2(80f, 0f));

        var hits = ring.Update(1f, Ship, [enemy]);

        Assert.Equal(1.5f, Assert.Single(hits).Damage);
    }

    [Fact]
    public void The_ring_does_not_touch_enemies_at_its_centre_or_at_twice_its_radius()
    {
        var ring = new OrbitingOrbs(5);
        var centre = Gemini(Vector2.Zero);
        var outside = Gemini(new Vector2(160f, 0f));

        var hits = ring.Update(5f, Ship, [centre, outside]);

        Assert.Empty(hits);
    }

    [Fact]
    public void Leaving_the_band_restarts_the_ring_second()
    {
        var ring = new OrbitingOrbs(5);
        var enemy = Gemini(new Vector2(80f, 0f));
        var away = Gemini(new Vector2(300f, 0f));

        Assert.Empty(ring.Update(0.75f, Ship, [enemy]));
        Assert.Empty(ring.Update(0.5f, Ship, [away]));
        Assert.Empty(ring.Update(0.75f, Ship, [enemy]));
        Assert.Single(ring.Update(0.25f, Ship, [enemy]));
    }

    private static (int Count, float Speed, float Orbit, float Radius, float Damage) Stats(OrbitingOrbs orbs) =>
        (orbs.OrbCount, orbs.RotationSpeedDeg, orbs.OrbitRadius, orbs.OrbRadius, orbs.Damage);
}
