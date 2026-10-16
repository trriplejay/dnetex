using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class GameSessionTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void Enemy_touching_the_ship_ends_the_game_and_freezes_it()
    {
        var session = new GameSession(Center, new Random(1));
        session.AddEnemy(EnemyRoster.Gemini.Create(Center + new Vector2(60, 0)));

        session.Update(Vector2.Zero, null, 0.1f, Width, Height);
        Assert.False(session.IsGameOver);

        for (var i = 0; i < 20 && !session.IsGameOver; i++)
            session.Update(Vector2.Zero, null, 0.05f, Width, Height);
        Assert.True(session.IsGameOver);

        var shipPosition = session.Ship.Position;
        var enemyPosition = session.Enemies[0].Position;
        session.Update(new Vector2(0, -1), null, 1f, Width, Height);
        Assert.Equal(shipPosition, session.Ship.Position);
        Assert.Equal(enemyPosition, session.Enemies[0].Position);
    }

    [Fact]
    public void Enemy_in_the_concave_gap_beside_the_ship_is_not_contact()
    {
        var session = new GameSession(Center, new Random(1));
        var enemy = TestEnemies.StationaryGemini(Center + new Vector2(50, -14));
        session.AddEnemy(enemy);

        session.Update(Vector2.Zero, null, 0.01f, Width, Height);

        Assert.False(session.IsGameOver);
    }

    [Fact]
    public void Lasers_damage_the_enemy_they_hit_and_are_used_up()
    {
        var session = TestEnemies.QuietSession(Center);
        var enemy = TestEnemies.StationaryGemini(Center + new Vector2(0, -150));
        session.AddEnemy(enemy);

        // One laser per second flies up from the nose into the enemy.
        for (var i = 0; i < 90 && enemy.HitPoints == 3; i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        Assert.Equal(2f, enemy.HitPoints);
        Assert.Empty(session.Lasers);
        Assert.False(session.IsGameOver);
    }

    [Fact]
    public void Enemy_with_no_hit_points_left_is_removed()
    {
        var session = TestEnemies.QuietSession(Center);
        var enemy = TestEnemies.StationaryGemini(Center + new Vector2(0, -150));
        session.AddEnemy(enemy);

        for (var i = 0; i < 60 * 5 && session.Enemies.Contains(enemy); i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        Assert.True(enemy.IsDead);
        Assert.DoesNotContain(enemy, session.Enemies);
    }

    [Fact]
    public void Lasers_fire_toward_the_aim_point_while_moving_in_another_direction()
    {
        var session = TestEnemies.QuietSession(Center);
        var target = TestEnemies.StationaryGemini(Center + new Vector2(300, 100));
        session.AddEnemy(target);

        // Fly up while aiming at the enemy down and to the right; aim follows it every frame.
        for (var i = 0; i < 150 && target.HitPoints == 3f; i++)
            session.Update(new Vector2(0, -1), target.Position, 1f / 60f, Width, Height);

        Assert.Equal(2f, target.HitPoints);
        Assert.True(session.Ship.Position.Y < Center.Y);
    }
    // ---- Weapon paths ----

    [Fact]
    public void Level_3_reached_one_level_at_a_time_offers_ballista_then_bola()
    {
        var session = TestEnemies.QuietSession(Center);

        KillFor(session, Leveling.Required(1));
        Assert.Equal(2, session.Leveling.Level);
        AssertGeneralOffer(session);
        session.ChooseUpgrade(0);

        KillFor(session, Leveling.Required(2));
        Assert.Equal(3, session.Leveling.Level);
        Assert.Equal([Upgrade.Ballista, Upgrade.Bola], session.UpgradeOffer);
        session.ChooseUpgrade(0);

        KillFor(session, Leveling.Required(3));
        Assert.Equal(4, session.Leveling.Level);
        AssertGeneralOffer(session);
    }

    [Fact]
    public void One_kill_from_level_2_to_4_offers_the_paths_then_general_cards()
    {
        var session = TestEnemies.QuietSession(Center);
        KillFor(session, Leveling.Required(1));
        session.ChooseUpgrade(0);

        KillFor(session, Leveling.Required(2) + Leveling.Required(3));

        Assert.Equal(4, session.Leveling.Level);
        Assert.Equal([Upgrade.Ballista, Upgrade.Bola], session.UpgradeOffer);
        session.ChooseUpgrade(1);
        AssertGeneralOffer(session);
        session.ChooseUpgrade(0);
        Assert.False(session.IsChoosingUpgrade);
    }

    [Fact]
    public void One_kill_from_level_1_to_4_offers_general_cards_either_side_of_the_paths()
    {
        var session = TestEnemies.QuietSession(Center);

        KillFor(session, Leveling.Required(1) + Leveling.Required(2) + Leveling.Required(3));

        Assert.Equal(4, session.Leveling.Level);
        AssertGeneralOffer(session);
        session.ChooseUpgrade(0);
        Assert.Equal([Upgrade.Ballista, Upgrade.Bola], session.UpgradeOffer);
        session.ChooseUpgrade(0);
        AssertGeneralOffer(session);
    }

    [Fact]
    public void Starting_at_level_5_still_offers_the_skipped_level_3_paths()
    {
        var session = new GameSession(Center, new Random(1), start: new StartOptions(StartLevel: 5));

        AssertGeneralOffer(session);
        session.ChooseUpgrade(0);
        Assert.Equal([Upgrade.Ballista, Upgrade.Bola], session.UpgradeOffer);
        session.ChooseUpgrade(1);
        AssertGeneralOffer(session);
        session.ChooseUpgrade(0);
        AssertGeneralOffer(session);
        session.ChooseUpgrade(0);
        Assert.False(session.IsChoosingUpgrade);
        Assert.Equal(WeaponPath.Bola, session.Stats.WeaponPath);
    }

    [Fact]
    public void Path_round_waits_for_a_pick_and_rejects_a_third_card()
    {
        var session = new GameSession(Center, new Random(1), start: new StartOptions(StartLevel: 3), roster: [], bosses: [], rings: []);
        session.ChooseUpgrade(0);
        Assert.Equal([Upgrade.Ballista, Upgrade.Bola], session.UpgradeOffer);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ChooseUpgrade(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ChooseUpgrade(-1));
        Assert.True(session.IsChoosingUpgrade);

        var ship = session.Ship.Position;
        session.Update(new Vector2(0, -1), null, 2f, Width, Height);
        Assert.Equal(0f, session.ElapsedSeconds);
        Assert.Equal(ship, session.Ship.Position);
        Assert.Empty(session.Lasers);
        Assert.Equal(WeaponPath.None, session.Stats.WeaponPath);
    }

    [Theory]
    [InlineData(0, WeaponPath.Ballista)]
    [InlineData(1, WeaponPath.Bola)]
    public void Picked_path_fires_for_the_rest_of_the_game(int pick, WeaponPath expected)
    {
        var session = TestEnemies.QuietSession(Center);
        KillFor(session, Leveling.Required(1));
        session.ChooseUpgrade(0);
        KillFor(session, Leveling.Required(2));
        session.ChooseUpgrade(pick);
        Assert.Equal(expected, session.Stats.WeaponPath);

        var fired = new List<Projectile>();
        for (var round = 0; round < 3; round++)
        {
            KillFor(session, Leveling.Required(session.Leveling.Level), fired);
            AssertGeneralOffer(session);
            session.ChooseUpgrade(0);
            for (var i = 0; i < 120; i++)
            {
                session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
                fired.AddRange(session.Lasers);
            }
        }

        // Applying the other path card later changes nothing either.
        Upgrades.Apply(expected == WeaponPath.Ballista ? Upgrade.Bola : Upgrade.Ballista, session.Stats);

        Assert.Equal(6, session.Leveling.Level);
        Assert.Equal(expected, session.Stats.WeaponPath);
        Assert.NotEmpty(fired);
        Assert.All(fired, projectile => Assert.Equal(expected == WeaponPath.Ballista ? typeof(Ballista) : typeof(Bola), projectile.GetType()));
    }

    [Theory]
    [InlineData(0, typeof(Ballista))]
    [InlineData(1, typeof(Bola))]
    public void Path_volley_fans_its_projectiles_at_the_raised_scale_and_pierce(int pick, Type kind)
    {
        var session = ChoosePath(pick);
        session.Stats.ProjectileCount = 3;
        session.Stats.Pierce = 2;
        session.Stats.ProjectileScale = 2f;
        session.Stats.Damage = 3f;

        session.Update(Vector2.Zero, null, 1f, Width, Height);

        Assert.Equal(3, session.Lasers.Count);
        Assert.All(session.Lasers, projectile =>
        {
            Assert.IsType(kind, projectile);
            Assert.Equal(2f, projectile.Scale);
            // A ballista bolt carries two more pierce than the player's; a bola carries exactly the player's.
            Assert.Equal(kind == typeof(Ballista) ? 4 : 2, projectile.PierceRemaining);
        });
        var headings = session.Lasers.Select(p => p.Heading).OrderBy(h => h).ToArray();
        Assert.Equal(-GameSession.VolleySpreadRadians, headings[0], precision: 5);
        Assert.Equal(0f, headings[1], precision: 5);
        Assert.Equal(GameSession.VolleySpreadRadians, headings[2], precision: 5);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    public void Path_projectile_deals_stat_damage_and_is_used_up_after_its_pierce_enemies(int pick, int pathPierceBonus)
    {
        var session = ChoosePath(pick);
        session.Stats.Pierce = 2;
        session.Stats.ProjectileScale = 2f;
        session.Stats.Damage = 3f;
        var tough = EnemyRoster.Gemini with { HitPoints = 10f };
        var nose = session.Ship.Nose;
        // A ballista bolt hits 3 + pierce enemies and a bola 1 + pierce; one more waits beyond either.
        var column = Enumerable.Range(0, 6)
            .Select(i => TestEnemies.Stationary(tough, nose - new Vector2(0, 100 + 50 * i)))
            .ToArray();
        foreach (var enemy in column)
            session.AddEnemy(enemy);

        // One volley at 1s; stop before the second one at 2s.
        for (var i = 0; i < 110; i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        var hits = pathPierceBonus + session.Stats.Pierce;
        Assert.Equal(column.Select((_, i) => i < hits ? 7f : 10f), column.Select(enemy => enemy.HitPoints));
        Assert.Empty(session.Lasers);
    }

    [Fact]
    public void Without_a_path_volleys_fire_the_unchanged_laser()
    {
        var session = TestEnemies.QuietSession(Center);

        session.Update(Vector2.Zero, null, 1f, Width, Height);

        var laser = Assert.IsType<Laser>(Assert.Single(session.Lasers));
        Assert.Equal(13f, laser.Length);
        Assert.Equal(4f, laser.Width);
        Assert.Equal(WeaponPath.None, session.Stats.WeaponPath);
    }

    /// <summary>A quiet session at level 3 with the given path card picked and play resumed.</summary>
    private static GameSession ChoosePath(int pick)
    {
        var session = new GameSession(Center, new Random(1), start: new StartOptions(StartLevel: 3), roster: [], bosses: [], rings: []);
        session.ChooseUpgrade(0);
        session.ChooseUpgrade(pick);
        Assert.False(session.IsChoosingUpgrade);
        return session;
    }

    /// <summary>Puts a stationary enemy worth the given experience in front of the ship and shoots it until a level-up offer appears.</summary>
    private static void KillFor(GameSession session, int experience, List<Projectile>? fired = null)
    {
        session.Stats.Damage = 1000f;
        var prize = EnemyRoster.Meta with { Experience = experience };
        session.AddEnemy(TestEnemies.Stationary(prize, session.Ship.Nose - new Vector2(0, 150)));
        for (var i = 0; i < 60 * 3 && !session.IsChoosingUpgrade; i++)
        {
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
            fired?.AddRange(session.Lasers);
        }
        Assert.True(session.IsChoosingUpgrade, "No level-up offer appeared");
    }

    private static void AssertGeneralOffer(GameSession session)
    {
        var offer = session.UpgradeOffer!;
        Assert.Equal(GameSession.UpgradeChoices, offer.Count);
        Assert.DoesNotContain(Upgrade.Ballista, offer);
        Assert.DoesNotContain(Upgrade.Bola, offer);
    }
}
