using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>Proximity Mines: drop, arming, blast, expiry, the 12-mine cap and the level-5 CLUSTER mini-mines.</summary>
public class ProximityMinesTests
{
    private static readonly Vector2 Spot = new(500, 400);

    [Fact]
    public void The_session_drops_a_mine_at_the_ship_and_with_no_enemies_mines_drop_and_expire_without_error()
    {
        var session = KillCreditTests.SessionWith(WeaponKind.ProximityMines);
        session.Update(new Vector2(1, 0), null, 0.5f, 1000f, 800f);

        for (var i = 0; i < 20 && session.Mines.Count == 0; i++)
            session.Update(Vector2.Zero, null, 0.1f, 1000f, 800f);

        var mine = Assert.Single(session.Mines);
        Assert.Equal(session.Ship.Position, mine.Position);
        Assert.Equal((3f, 60f), (mine.Damage, mine.BlastRadius));

        // 0.5 drops/s with nothing to touch them: after 12 s more, the oldest have expired and none exploded.
        for (var i = 0; i < 120; i++)
            session.Update(Vector2.Zero, null, 0.1f, 1000f, 800f);
        Assert.DoesNotContain(mine, session.Mines);
        Assert.InRange(session.Mines.Count, 4, 6);
        Assert.Empty(session.MineExplosions);
    }

    [Fact]
    public void A_mine_ignores_a_touch_at_0_4_s_and_at_0_6_s_explodes_hitting_every_enemy_within_60_px_but_not_61()
    {
        var mines = new ProximityMines();
        mines.Drop(new OwnedWeapon(WeaponKind.ProximityMines), [Spot]);
        var toucher = Gemini(Spot);
        var atEdge = Gemini(Spot + new Vector2(60, 0));
        var outside = Gemini(Spot + new Vector2(0, 61));
        Enemy[] enemies = [toucher, atEdge, outside];

        mines.Update(0.2f, enemies, Hit);
        mines.Update(0.2f, enemies, Hit);
        Assert.Single(mines.Mines);
        Assert.False(mines.Mines[0].IsArmed);
        Assert.Empty(mines.Explosions);
        Assert.All(enemies, enemy => Assert.Equal(10f, enemy.HitPoints));

        mines.Update(0.2f, enemies, Hit);

        Assert.Empty(mines.Mines);
        Assert.Equal(new Circle(Spot, 60f), Assert.Single(mines.Explosions));
        Assert.Equal(7f, toucher.HitPoints);
        Assert.Equal(7f, atEdge.HitPoints);
        Assert.Equal(10f, outside.HitPoints);

        // The blast hit each once: nothing more happens afterwards.
        mines.Update(0.2f, enemies, Hit);
        Assert.Empty(mines.Explosions);
        Assert.Equal(7f, toucher.HitPoints);
    }

    [Fact]
    public void An_untouched_mine_is_gone_after_10_s()
    {
        var mines = new ProximityMines();
        mines.Drop(new OwnedWeapon(WeaponKind.ProximityMines), [Spot]);

        for (var i = 0; i < 99; i++)
            mines.Update(0.1f, [], Hit);
        Assert.Single(mines.Mines);

        mines.Update(0.1f, [], Hit);
        Assert.Empty(mines.Mines);
        Assert.Empty(mines.Explosions);
    }

    [Fact]
    public void A_13th_mine_removes_the_oldest_leaving_12()
    {
        var mines = new ProximityMines();
        var weapon = new OwnedWeapon(WeaponKind.ProximityMines);
        var spots = Enumerable.Range(0, 13).Select(i => Spot + new Vector2(30 * i, 0)).ToArray();

        foreach (var spot in spots)
            mines.Drop(weapon, [spot]);

        Assert.Equal(12, mines.Mines.Count);
        Assert.Equal(spots.Skip(1), mines.Mines.Select(mine => mine.Position));
    }

    [Fact]
    public void A_level_5_explosion_throws_3_mini_mines_50_px_out_that_go_off_0_3_s_later_at_half_damage_and_radius_once()
    {
        var mines = new ProximityMines();
        mines.Drop(MinesAtLevel(5), [Spot]);
        var toucher = Gemini(Spot, hitPoints: 1000f);
        // 50 px out to the right lies the first mini-mine; these two are beyond the 60 px main blast.
        var inMiniBlast = Gemini(Spot + new Vector2(50 + 29, 0));
        var outsideMiniBlast = Gemini(Spot + new Vector2(0, -50 - 31));
        Enemy[] enemies = [toucher, inMiniBlast, outsideMiniBlast];

        Arm(mines, enemies);
        Assert.Single(mines.Explosions);
        Assert.Equal(3, mines.MiniMines.Count);
        Assert.All(mines.MiniMines, mini =>
        {
            Assert.Equal(50f, Vector2.Distance(Spot, mini.Position), 3);
            Assert.Equal((1.5f, 30f), (mini.Damage, mini.BlastRadius));
        });
        Assert.Equal(1000f - 3f, toucher.HitPoints);
        Assert.Equal(10f, inMiniBlast.HitPoints);

        mines.Update(0.1f, enemies, Hit);
        mines.Update(0.1f, enemies, Hit);
        Assert.Equal(3, mines.MiniMines.Count);
        Assert.Empty(mines.Explosions);

        mines.Update(0.1f, enemies, Hit);
        Assert.Equal(3, mines.Explosions.Count);
        Assert.All(mines.Explosions, blast => Assert.Equal(30f, blast.Radius));
        Assert.Equal(10f - 1.5f, inMiniBlast.HitPoints);
        Assert.Equal(10f, outsideMiniBlast.HitPoints);
        // Each mini-mine is 50 px from the toucher, outside its 30 px blast.
        Assert.Equal(1000f - 3f, toucher.HitPoints);

        // Mini-mine detonations throw nothing more.
        Assert.Empty(mines.MiniMines);
        for (var i = 0; i < 10; i++)
            mines.Update(0.1f, enemies, Hit);
        Assert.Empty(mines.MiniMines);
        Assert.Equal(10f - 1.5f, inMiniBlast.HitPoints);
    }

    [Fact]
    public void A_level_4_explosion_throws_no_mini_mines()
    {
        var mines = new ProximityMines();
        mines.Drop(MinesAtLevel(4), [Spot]);

        Arm(mines, [Gemini(Spot, hitPoints: 1000f)]);

        Assert.Single(mines.Explosions);
        Assert.Empty(mines.MiniMines);
    }

    /// <summary>Runs the mine past its arming time with the enemies in place, until it explodes.</summary>
    private static void Arm(ProximityMines mines, IReadOnlyList<Enemy> enemies)
    {
        for (var i = 0; i < 6 && mines.Explosions.Count == 0; i++)
            mines.Update(0.1f, enemies, Hit);
        Assert.NotEmpty(mines.Explosions);
    }

    private static OwnedWeapon MinesAtLevel(int level)
    {
        var weapon = new OwnedWeapon(WeaponKind.ProximityMines);
        for (var i = 1; i < level; i++)
            weapon.RaiseLevel();
        Assert.Equal(level, weapon.Level);
        return weapon;
    }

    private static void Hit(Enemy enemy, float damage) => enemy.TakeDamage(damage);

    private static Enemy Gemini(Vector2 position, float hitPoints = 10f) =>
        TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = hitPoints }, position);
}
