using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>The Ricochet Disc's throw, flight and per-leg hits.</summary>
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

        for (var i = 0; i < 24; i++)
            disc.Update(0.025f, Origin);
        Assert.False(disc.IsReturning);
        AssertNear(Origin + new Vector2(0, -240), disc.Position);

        disc.Update(0.025f, Origin);
        Assert.True(disc.IsReturning);
        AssertNear(Origin + new Vector2(0, -250), disc.Position);

        // The ship has moved right; the disc flies to where it is now and is caught there.
        var ship = Origin + new Vector2(150, 0);
        for (var i = 0; i < 120 && !disc.IsCaught; i++)
            disc.Update(Frame, ship);
        Assert.True(disc.IsCaught);
        Assert.Equal(ship, disc.Position);
    }

    [Fact]
    public void Each_enemy_in_a_line_loses_exactly_the_disc_damage_once_on_the_way_out_and_once_on_the_way_back()
    {
        var disc = Assert.Single(RicochetDisc.Throw(new OwnedWeapon(WeaponKind.RicochetDisc), Origin, Up, []));
        Enemy[] line = [Gemini(Origin + new Vector2(0, -60)), Gemini(Origin + new Vector2(0, -120)), Gemini(Origin + new Vector2(0, -180))];

        // Small frames keep each enemy in contact over several frames of each leg.
        var contactFrames = 0;
        while (!disc.IsReturning)
        {
            disc.Update(Frame, Origin);
            if (!disc.IsReturning)
                contactFrames += line.Count(enemy => Touching(disc, enemy));
            disc.Strike(line, Hit);
        }
        Assert.True(contactFrames > 3 * 2, "Each enemy should stay in contact for several frames");
        Assert.All(line, enemy => Assert.Equal(10f - 2f, enemy.HitPoints));

        while (!disc.IsCaught)
        {
            disc.Update(Frame, Origin);
            disc.Strike(line, Hit);
        }
        Assert.All(line, enemy => Assert.Equal(10f - 4f, enemy.HitPoints));
    }

    [Fact]
    public void An_enemy_at_the_far_point_is_hit_once_out_and_once_back_while_it_stays_in_contact()
    {
        var disc = Assert.Single(RicochetDisc.Throw(new OwnedWeapon(WeaponKind.RicochetDisc), Origin, Up, []));
        var atTurn = Gemini(Origin + new Vector2(0, -250));

        while (!disc.IsCaught)
        {
            disc.Update(Frame, Origin);
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
    public void A_level_20_disc_flies_out_and_back_and_enemies_around_its_far_point_off_its_path_keep_full_hit_points()
    {
        var disc = Assert.Single(RicochetDisc.Throw(DiscAtLevel(20), Origin, Up, []));
        var farPoint = Origin + new Vector2(0, -250);
        // Off the disc's path (radius 12 along x = 500), 60 px from its far point on four sides.
        Enemy[] around =
        [
            Gemini(farPoint + new Vector2(0, -60)),
            Gemini(farPoint + new Vector2(60, 0)),
            Gemini(farPoint + new Vector2(-60, 0)),
            Gemini(farPoint + new Vector2(-60, 60)),
        ];
        var turned = false;

        for (var i = 0; i < 240 && !disc.IsCaught; i++)
        {
            disc.Update(Frame, Origin);
            turned |= disc.IsReturning;
            disc.Strike(around, Hit);
        }

        Assert.True(turned);
        Assert.True(disc.IsCaught);
        Assert.Equal(Origin, disc.Position);
        Assert.All(around, enemy => Assert.Equal(10f, enemy.HitPoints));
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
