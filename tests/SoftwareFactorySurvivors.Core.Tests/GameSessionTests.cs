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
}
