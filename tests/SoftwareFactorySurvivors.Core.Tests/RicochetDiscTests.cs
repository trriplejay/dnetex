using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>The Ricochet Disc's throw, flight, per-leg hits and its level-5 SHATTER.</summary>
public class RicochetDiscTests
{
    private const float Frame = 1f / 60f;
    private static readonly Vector2 Origin = new(500, 400);
    private static readonly Vector2 Up = new(0, -1);

    [Fact]
    public void A_disc_heads_for_the_nearest_live_enemy_and_along_forward_when_there_is_none()
    {
        var weapon = new OwnedWeapon(WeaponKind.RicochetDisc);
        var nearest = Gemini(Origin + new Vector2(100, 100));
        var dead = Gemini(Origin + new Vector2(-10, 0));
        dead.TakeDamage(1000f);
        Enemy[] enemies = [Gemini(Origin + new Vector2(0, -300)), nearest, dead];

        var aimed = Assert.Single(RicochetDisc.Throw(weapon, Origin, Up, enemies));
        AssertNear(Vector2.Normalize(new Vector2(1, 1)), aimed.Direction);

        var straight = Assert.Single(RicochetDisc.Throw(weapon, Origin, Up, [dead]));
        AssertNear(Up, straight.Direction);
        Assert.Equal(Origin, straight.Position);
        Assert.Equal(2f, straight.Damage);
    }

    [Fact]
    public void A_disc_is_250_px_out_and_turns_back_after_0_625_s_then_returns_to_the_moving_ship()
    {
        var disc = Assert.Single(RicochetDisc.Throw(new OwnedWeapon(WeaponKind.RicochetDisc), Origin, Up, []));
        var shards = new List<DiscShard>();

        for (var i = 0; i < 24; i++)
            disc.Update(0.025f, Origin, shards);
        Assert.False(disc.IsReturning);
        AssertNear(Origin + new Vector2(0, -240), disc.Position);

        disc.Update(0.025f, Origin, shards);
        Assert.True(disc.IsReturning);
        AssertNear(Origin + new Vector2(0, -250), disc.Position);
        Assert.Empty(shards);

        // The ship has moved right; the disc flies to where it is now and is caught there.
        var ship = Origin + new Vector2(150, 0);
        for (var i = 0; i < 120 && !disc.IsCaught; i++)
            disc.Update(Frame, ship, shards);
        Assert.True(disc.IsCaught);
        Assert.Equal(ship, disc.Position);
    }

    [Fact]
    public void Each_enemy_in_a_line_loses_exactly_the_disc_damage_once_on_the_way_out_and_once_on_the_way_back()
    {
        var disc = Assert.Single(RicochetDisc.Throw(new OwnedWeapon(WeaponKind.RicochetDisc), Origin, Up, []));
        Enemy[] line = [Gemini(Origin + new Vector2(0, -60)), Gemini(Origin + new Vector2(0, -120)), Gemini(Origin + new Vector2(0, -180))];
        var shards = new List<DiscShard>();

        // Small frames keep each enemy in contact over several frames of each leg.
        var contactFrames = 0;
        while (!disc.IsReturning)
        {
            disc.Update(Frame, Origin, shards);
            if (!disc.IsReturning)
                contactFrames += line.Count(enemy => Touching(disc, enemy));
            disc.Strike(line, Hit);
        }
        Assert.True(contactFrames > 3 * 2, "Each enemy should stay in contact for several frames");
        Assert.All(line, enemy => Assert.Equal(10f - 2f, enemy.HitPoints));

        while (!disc.IsCaught)
        {
            disc.Update(Frame, Origin, shards);
            disc.Strike(line, Hit);
        }
        Assert.All(line, enemy => Assert.Equal(10f - 4f, enemy.HitPoints));
    }

    [Fact]
    public void An_enemy_at_the_far_point_is_hit_once_out_and_once_back_while_it_stays_in_contact()
    {
        var disc = Assert.Single(RicochetDisc.Throw(new OwnedWeapon(WeaponKind.RicochetDisc), Origin, Up, []));
        var atTurn = Gemini(Origin + new Vector2(0, -250));
        var shards = new List<DiscShard>();

        while (!disc.IsCaught)
        {
            disc.Update(Frame, Origin, shards);
            disc.Strike([atTurn], Hit);
        }

        Assert.Equal(10f - 4f, atTurn.HitPoints);
    }

    [Fact]
    public void At_level_1_the_session_throws_one_disc_every_2_seconds()
    {
        var session = KillCreditTests.SessionWith(WeaponKind.RicochetDisc);
        var thrown = new HashSet<RicochetDisc>();
        var firstThrowFrame = -1;

        for (var frame = 1; frame <= 6 * 60 + 1; frame++)
        {
            session.Update(Vector2.Zero, null, Frame, 1000f, 800f);
            foreach (var disc in session.Discs)
            {
                if (thrown.Add(disc) && firstThrowFrame < 0)
                    firstThrowFrame = frame;
            }
        }

        Assert.Equal(120, firstThrowFrame);
        Assert.Equal(3, thrown.Count);
        // With no enemy the session throws along the ship's Forward (up).
        AssertNear(Up, thrown.First().Direction);
    }

    [Fact]
    public void A_level_5_disc_shatters_into_4_half_damage_shards_at_its_far_point_and_still_returns()
    {
        var weapon = DiscAtLevel(5);
        weapon.Damage = 6f;
        var disc = Assert.Single(RicochetDisc.Throw(weapon, Origin, Up, []));
        var shards = new List<DiscShard>();

        while (!disc.IsReturning)
            disc.Update(Frame, Origin, shards);

        Assert.Equal(4, shards.Count);
        Assert.All(shards, shard =>
        {
            Assert.Equal(3f, shard.Damage);
            Assert.Equal(0, shard.Pierce);
            AssertNear(Origin + new Vector2(0, -250), shard.Position);
        });
        // Flying outward: one straight on, the others spread evenly around.
        AssertNear(Up, shards[0].Direction);
        Assert.Equal(4, shards.Select(shard => (MathF.Round(shard.Direction.X), MathF.Round(shard.Direction.Y))).Distinct().Count());

        for (var i = 0; i < 120 && !disc.IsCaught; i++)
            disc.Update(Frame, Origin, shards);
        Assert.True(disc.IsCaught);
        Assert.Equal(4, shards.Count);
    }

    [Fact]
    public void A_level_4_disc_bursts_no_shards()
    {
        var disc = Assert.Single(RicochetDisc.Throw(DiscAtLevel(4), Origin, Up, []));
        var shards = new List<DiscShard>();

        while (!disc.IsCaught)
            disc.Update(Frame, Origin, shards);

        Assert.Empty(shards);
    }

    [Fact]
    public void A_shard_is_spent_on_its_first_hit()
    {
        var shard = new DiscShard(Origin, Up, 1.5f);
        var first = Gemini(Origin + new Vector2(0, -60));
        var behind = Gemini(Origin + new Vector2(0, -100));

        for (var i = 0; i < 60 && !shard.IsGone; i++)
        {
            shard.Update(Frame);
            shard.Strike([first, behind], Hit);
        }

        Assert.True(shard.IsSpent);
        Assert.Equal(10f - 1.5f, first.HitPoints);
        Assert.Equal(10f, behind.HitPoints);
    }

    [Fact]
    public void A_shard_that_hits_nothing_is_gone_after_200_px()
    {
        var shard = new DiscShard(Origin, Up, 1f);

        for (var i = 0; i < 19; i++)
            shard.Update(0.025f);
        Assert.False(shard.IsExpired);

        shard.Update(0.025f);
        Assert.True(shard.IsExpired);
        Assert.False(shard.IsSpent);
        AssertNear(Origin + new Vector2(0, -200), shard.Position);
        var beyond = Gemini(Origin + new Vector2(0, -210));
        shard.Strike([beyond], Hit);
        Assert.Equal(10f, beyond.HitPoints);
        shard.Update(0.1f);
        AssertNear(Origin + new Vector2(0, -200), shard.Position);
    }

    private static OwnedWeapon DiscAtLevel(int level)
    {
        var weapon = new OwnedWeapon(WeaponKind.RicochetDisc);
        for (var i = 1; i < level; i++)
            weapon.RaiseLevel();
        Assert.Equal(level, weapon.Level);
        return weapon;
    }

    private static bool Touching(RicochetDisc disc, Enemy enemy) =>
        Collision.CircleTouches(disc.Position, disc.Radius, enemy.GetStrokes());

    private static void Hit(Enemy enemy, float damage) => enemy.TakeDamage(damage);

    private static Enemy Gemini(Vector2 position) =>
        TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 10f }, position);

    private static void AssertNear(Vector2 expected, Vector2 actual, float tolerance = 0.01f) =>
        Assert.True(Vector2.Distance(expected, actual) <= tolerance, $"Expected {expected}, got {actual}");
}
