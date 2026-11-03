using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>Proximity Mines: drop, arming, blast, expiry and the 12-mine cap.</summary>
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
    public void A_level_20_mine_explodes_once_with_its_normal_blast_and_nothing_follows_beyond_it()
    {
        var mines = new ProximityMines();
        var weapon = MinesAtLevel(20);
        mines.Drop(weapon, [Spot]);
        var toucher = Gemini(Spot, hitPoints: 1000f);
        // Beyond the 60 px blast, on the three spots 50 px out where thrown mini-mines would have landed and reached.
        var beyond = Enumerable.Range(0, 3)
            .Select(i => i * MathF.Tau / 3f)
            .Select(angle => Gemini(Spot + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (weapon.BlastRadius + 15f)))
            .ToArray();
        Enemy[] enemies = [toucher, .. beyond];

        Arm(mines, enemies);
        Assert.Equal(new Circle(Spot, weapon.BlastRadius), Assert.Single(mines.Explosions));
        Assert.Equal(1000f - weapon.Damage, toucher.HitPoints);

        for (var i = 0; i < 10; i++)
        {
            mines.Update(0.1f, enemies, Hit);
            Assert.Empty(mines.Explosions);
        }

        Assert.Empty(mines.Mines);
        Assert.All(beyond, enemy => Assert.Equal(10f, enemy.HitPoints));
        Assert.Equal(1000f - weapon.Damage, toucher.HitPoints);
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
