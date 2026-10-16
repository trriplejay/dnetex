using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class BulletBurstsTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void BulletBursts_bullet_speed_is_strictly_slower_than_ship_speed()
    {
        var ability = new BulletBursts();
        Assert.True(ability.Speed < Ship.Speed, $"Bullet speed {ability.Speed} must be strictly less than Ship.Speed {Ship.Speed}");

        // Attempting to construct with bulletSpeed >= Ship.Speed throws
        Assert.Throws<ArgumentOutOfRangeException>(() => new BulletBursts(bulletSpeed: Ship.Speed));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BulletBursts(bulletSpeed: Ship.Speed + 50f));
    }

    [Fact]
    public void BulletBursts_over_many_seeded_cycles_asserts_speed_and_angular_gap_exceeds_ship_diameter_at_max_range()
    {
        var minRequiredGap = BulletBursts.ShipDiameter / BulletBursts.MaxPlayFieldRadius;
        var dummyBoss = TestEnemies.Stationary(EnemyRoster.ClaudeBoss, Center);

        for (var seed = 0; seed < 50; seed++)
        {
            var rng = new Random(seed);
            var ability = new BulletBursts(
                secondsBetweenBursts: 1.0f,
                bulletsPerBurst: 16,
                bulletSpeed: 120f,
                random: rng);

            Assert.True(ability.Speed < Ship.Speed);

            // Run for 10 bursts
            for (var cycle = 0; cycle < 10; cycle++)
            {
                ability.Update(dummyBoss, Center, 1.0f, rng);
                var hazards = ability.TakeHazards();
                Assert.NotEmpty(hazards);
                Assert.Equal(16, hazards.Count);
                Assert.All(hazards, h =>
                {
                    Assert.Equal(HazardKind.Bullet, h.Kind);
                    Assert.True(h.ArmSeconds >= 1.0f);
                });

                // Extract bullet initial movement directions by stepping dt=0.1
                var angles = new List<float>();
                foreach (var hazard in hazards)
                {
                    var initialCircle = Assert.Single(hazard.Shape.Circles);
                    var initialPos = initialCircle.Center;
                    hazard.Update(0.1f);
                    var nextCircle = Assert.Single(hazard.Shape.Circles);
                    var velocity = (nextCircle.Center - initialPos) / 0.1f;
                    var speed = velocity.Length();

                    Assert.InRange(speed, ability.Speed - 0.1f, ability.Speed + 0.1f);
                    Assert.True(speed < Ship.Speed);

                    var angle = MathF.Atan2(velocity.Y, velocity.X);
                    // Normalize to [0, 2pi)
                    while (angle < 0f) angle += MathF.Tau;
                    while (angle >= MathF.Tau) angle -= MathF.Tau;
                    angles.Add(angle);
                }

                angles.Sort();

                // Compute all angular gaps between adjacent bullets around the circle
                var maxGap = 0f;
                for (var i = 0; i < angles.Count; i++)
                {
                    var nextIdx = (i + 1) % angles.Count;
                    var diff = (nextIdx == 0)
                        ? (MathF.Tau - angles[i] + angles[0])
                        : (angles[nextIdx] - angles[i]);
                    if (diff > maxGap)
                        maxGap = diff;
                }

                // Assert an angular gap wider than shipDiameter / maxRange exists
                Assert.True(
                    maxGap > minRequiredGap,
                    $"Max gap {maxGap} rad was not greater than required minimum {minRequiredGap} rad");

                // Also ensure generous gap >= 60 degrees (PI / 3) minus epsilon
                Assert.True(maxGap >= (MathF.PI / 3f) - 0.05f);
            }
        }
    }

    [Fact]
    public void Armed_bullet_touching_ship_in_GameSession_costs_exactly_one_hit_point()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 3;

        var bullet = Hazard.CreateBullet(Center, radius: 10f, lifetimeSeconds: 4.0f, armSeconds: 1.0f);
        session.AddHazard(bullet);

        // Before arming (e.g. 0.5s), no damage
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.Equal(3, session.Stats.HitPoints);
        Assert.False(bullet.IsArmed);

        // Advance to 1.05s total (bullet is now armed) -> contact occurs and costs exactly 1 HP
        session.Update(Vector2.Zero, null, 0.55f, Width, Height);
        Assert.True(bullet.IsArmed);
        Assert.Equal(2, session.Stats.HitPoints);
        Assert.True(session.InvulnerableSecondsLeft > 0f);

        // Bullet expires on contact per ExpireOnContact = true
        Assert.DoesNotContain(bullet, session.Hazards);
    }
}
