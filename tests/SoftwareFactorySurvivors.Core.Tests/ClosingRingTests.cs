using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ClosingRingTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void ClosingRing_has_finite_contraction_speed_and_never_drops_below_minimum_radius_larger_than_ship_diameter()
    {
        var ability = new ClosingRing(
            initialRadius: 200f,
            minRadius: 60f,
            contractionSpeed: 50f,
            warmupSeconds: 1.2f,
            lifetimeSeconds: 10f);

        Assert.True(ability.ContractionSpeed > 0f);
        // Ship diameter is ~48px (nose +24 to tail -24). Min radius must be > 48px.
        Assert.True(ability.MinRadius > 48f);
        Assert.True(ability.MinRadius >= ClosingRing.DefaultMinRadius);

        // Even if constructed with a tiny minRadius <= ship diameter, it clamps to >= DefaultMinRadius
        var clampedAbility = new ClosingRing(minRadius: 10f);
        Assert.True(clampedAbility.MinRadius >= ClosingRing.DefaultMinRadius);
        Assert.True(clampedAbility.MinRadius > 48f);

        // Verify the created hazard contracts over time down to minRadius and never below
        var hazard = ClosingRing.CreateRingHazard(Center, initialRadius: 100f, minRadius: 60f, contractionSpeed: 50f, warmupSeconds: 1.0f, lifetimeSeconds: 5f);
        Assert.Equal(HazardKind.Ring, hazard.Kind);
        Assert.Equal(1.0f, hazard.ArmSeconds);

        // Update 0.5s: radius drops from 100 to 75
        hazard.Update(0.5f);
        Assert.NotEmpty(hazard.Shape.Lines);
        var radiusAtHalfSec = Vector2.Distance(Center, hazard.Shape.Lines[0].A);
        Assert.Equal(75f, radiusAtHalfSec, precision: 2);

        // Update 1.0s more: 75 - 50 = 25, but clamped to minRadius 60
        hazard.Update(1.0f);
        var radiusClamped = Vector2.Distance(Center, hazard.Shape.Lines[0].A);
        Assert.Equal(60f, radiusClamped, precision: 2);

        // Further updates keep radius strictly at minRadius
        hazard.Update(2.0f);
        var radiusLater = Vector2.Distance(Center, hazard.Shape.Lines[0].A);
        Assert.Equal(60f, radiusLater, precision: 2);
    }

    [Fact]
    public void Ring_hazard_warmup_guard_enforces_at_least_one_second_arm_time()
    {
        // Zero warmup request is clamped to >= 1.0s
        var hazard = ClosingRing.CreateRingHazard(Center, warmupSeconds: 0f);
        Assert.True(hazard.ArmSeconds >= 1.0f);
        Assert.False(hazard.IsArmed);

        var ability = new ClosingRing(warmupSeconds: 0f);
        Assert.True(ability.WarmupSeconds >= 1.0f);
    }

    [Fact]
    public void Ship_on_band_during_warmup_takes_no_damage()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 3f;
        session.Stats.HitPoints = 3f;

        // Create ring directly on the ship (radius = 0 or band intersecting ship)
        // With Center at ship's Position, radius = 10f puts circle right on ship strokes
        var hazard = ClosingRing.CreateRingHazard(Center, initialRadius: 10f, minRadius: 10f, contractionSpeed: 0f, warmupSeconds: 1.5f, lifetimeSeconds: 5f);
        session.AddHazard(hazard);

        // Advance 0.5s (< 1.5s): unarmed, takes no damage
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.Equal(3, session.Stats.HitPoints);
        Assert.False(hazard.IsArmed);

        // Advance another 0.6s (total 1.1s < 1.5s): still unarmed, takes no damage
        session.Update(Vector2.Zero, null, 0.6f, Width, Height);
        Assert.Equal(3, session.Stats.HitPoints);
        Assert.False(hazard.IsArmed);
    }

    [Fact]
    public void Ship_on_the_armed_ring_band_loses_six_times_the_time_scale_each_tick_and_the_ring_stays()
    {
        var session = TestEnemies.QuietSession(Center);

        // Ring centred at Center + (200, 0), stationary radius 100: its band passes through x = Center.X + 100.
        var ringCenter = Center + new Vector2(200, 0);
        var hazard = ClosingRing.CreateRingHazard(ringCenter, initialRadius: 100f, minRadius: 100f, contractionSpeed: 0f, warmupSeconds: 1.0f, lifetimeSeconds: 10f);
        Assert.Equal(6f, hazard.ContactDamage);
        session.AddHazard(hazard);

        // Armed while the ship is 100 px clear of the band: nothing.
        session.Update(Vector2.Zero, null, 1.1f, Width, Height);
        Assert.True(hazard.IsArmed);
        Assert.Equal(100f, session.Stats.HitPoints);

        // Fly onto the band (200 px/s for 0.5 s = 100 px): one tick at once.
        session.Update(new Vector2(1, 0), null, 0.5f, Width, Height);
        var expected = 100f - EnemyRoster.ScaledContactDamage(Hazard.RingContactDamage, session.ElapsedSeconds);
        Assert.Equal(expected, session.Stats.HitPoints, precision: 2);

        // Held on the band: another tick every 0.5 s, and the ring stays in play.
        for (var tick = 0; tick < 3; tick++)
        {
            session.Update(Vector2.Zero, null, 0.5f, Width, Height);
            expected -= EnemyRoster.ScaledContactDamage(Hazard.RingContactDamage, session.ElapsedSeconds);
            Assert.Equal(expected, session.Stats.HitPoints, precision: 2);
            Assert.Contains(hazard, session.Hazards);
        }
    }

    [Fact]
    public void Ship_staying_strictly_inside_or_outside_the_band_takes_no_damage()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 3f;
        session.Stats.HitPoints = 3f;

        // Place ring far away: radius 50, center at (100, 100) -> ship is strictly outside
        var hazard = ClosingRing.CreateRingHazard(new Vector2(100, 100), initialRadius: 50f, minRadius: 50f, contractionSpeed: 0f, warmupSeconds: 1.0f, lifetimeSeconds: 5f);
        session.AddHazard(hazard);

        // Advance past arming
        session.Update(Vector2.Zero, null, 2.0f, Width, Height);
        Assert.True(hazard.IsArmed);
        Assert.Equal(3, session.Stats.HitPoints);
    }

    [Fact]
    public void Ship_held_inside_the_minimum_radius_takes_no_damage_over_the_rings_whole_life()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 5f;
        session.Stats.HitPoints = 5f;

        // Ring centred at Center (ship position), initialRadius 200, contracting at 50/s down to minRadius 70 (> ship diameter ~48px)
        // Lifetime is 4.0s, warmup is 1.0s
        var hazard = ClosingRing.CreateRingHazard(
            Center,
            initialRadius: 200f,
            minRadius: 70f,
            contractionSpeed: 50f,
            warmupSeconds: 1.0f,
            lifetimeSeconds: 4.0f);
        session.AddHazard(hazard);

        // Advance through whole life in 0.5s steps
        // Ship remains at Center (inside the ring, strictly smaller than minRadius 70)
        for (var t = 0f; t < 5.0f; t += 0.5f)
        {
            session.Update(Vector2.Zero, null, 0.5f, Width, Height);
            Assert.Equal(5, session.Stats.HitPoints);
        }

        // Hazard is now expired and pruned from session
        Assert.DoesNotContain(hazard, session.Hazards);
        Assert.Equal(5, session.Stats.HitPoints);
        Assert.False(session.IsGameOver);
    }

    [Fact]
    public void ClosingRing_ability_emits_hazards_periodically_and_integrates_with_GameSession()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = 5f;
        session.Stats.HitPoints = 5f;

        var ability = new ClosingRing(
            initialRadius: 150f,
            minRadius: 65f,
            contractionSpeed: 30f,
            warmupSeconds: 1.0f,
            lifetimeSeconds: 3.0f,
            cooldownSeconds: 4.0f);

        var bossType = EnemyRoster.ClaudeBoss with { CreateAbility = () => ability };
        // Hold the boss still: its Dash lunges (420 px/s) would otherwise reach the ship within these
        // seconds and cost a contact hit, which is not what this ring test is about.
        var boss = TestEnemies.Stationary(bossType, new Vector2(800, 400));
        session.AddEnemy(boss);

        // First frame: ability triggers initial ring hazard
        session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
        var hazard = Assert.Single(session.Hazards);
        Assert.Equal(HazardKind.Ring, hazard.Kind);
        Assert.False(hazard.IsArmed);

        // Ship stays in center, taking no damage as ring contracts down to 65
        for (var i = 0; i < 180; i++) // 3 seconds
        {
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
            Assert.Equal(5, session.Stats.HitPoints);
        }

        // Around 3.0s, hazard expires
        // At 4.0s, second ring spawns
        for (var i = 0; i < 80; i++)
        {
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
        }

        Assert.NotEmpty(session.Hazards);
        Assert.Equal(5, session.Stats.HitPoints);
    }
}
