using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ScoringTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    [Theory]
    [InlineData(0f, "00:00")]
    [InlineData(9.99f, "00:09")]
    [InlineData(83.9f, "01:23")]
    [InlineData(599.99f, "09:59")]
    [InlineData(600f, "10:00")]
    public void Clock_shows_whole_minutes_and_seconds(float seconds, string expected)
    {
        Assert.Equal(expected, GameClock.Format(seconds));
    }

    [Fact]
    public void Surviving_ten_minutes_wins_and_stops_the_clock_at_ten()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [new BossArrival(600f, EnemyRoster.ClaudeBoss)]);

        session.Update(Vector2.Zero, null, 600f, Width, Height);
        var claudeBoss = Assert.Single(session.Bosses);
        Assert.Same(EnemyRoster.ClaudeBoss, claudeBoss.Type);

        // At 600+ s with ClaudeBoss alive, IsVictory is false and ElapsedSeconds == 600 with GameClock.Format == "10:00" over several Updates
        for (var i = 0; i < 3; i++)
        {
            session.Update(Vector2.Zero, null, 1f, Width, Height);
            Assert.False(session.IsVictory);
            Assert.False(session.IsGameOver);
            Assert.Equal(GameSession.SurvivalSeconds, session.ElapsedSeconds);
            Assert.Equal("10:00", GameClock.Format(session.ElapsedSeconds));
        }

        // Killing ClaudeBoss sets IsVictory on that Update with IsGameOver false
        claudeBoss.TakeDamage(claudeBoss.HitPoints);
        session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        Assert.True(session.IsVictory);
        Assert.False(session.IsGameOver);
        Assert.Equal(GameSession.SurvivalSeconds, session.ElapsedSeconds);

        // A session where only GeminiBoss dies is not a victory
        var geminiSession = new GameSession(Center, new Random(2), roster: [], bosses: [new BossArrival(180f, EnemyRoster.GeminiBoss)]);
        geminiSession.Update(Vector2.Zero, null, 180f, Width, Height);
        var gemini = Assert.Single(geminiSession.Bosses);
        foreach (var shield in gemini.Shields)
            shield.TakeDamage(shield.HitPoints);
        gemini.TakeDamage(gemini.HitPoints);
        geminiSession.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
        Assert.False(geminiSession.IsVictory);
    }

    [Fact]
    public void Being_touched_on_the_last_frame_is_a_loss_not_a_win()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Update(Vector2.Zero, null, 599.9f, Width, Height);
        session.AddEnemy(EnemyRoster.Gemini.Create(Center));

        session.Update(Vector2.Zero, null, 0.5f, Width, Height);

        Assert.True(session.IsGameOver);
        Assert.False(session.IsVictory);
    }

    [Fact]
    public void Killing_an_enemy_scores_one_point_but_damaging_it_does_not()
    {
        var session = TestEnemies.QuietSession(Center);
        var enemy = TestEnemies.StationaryGemini(Center + new Vector2(0, -150));
        session.AddEnemy(enemy);

        // One laser per second flies up from the nose into the enemy.
        for (var i = 0; i < 120 && enemy.HitPoints == 3; i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
        Assert.Equal(2f, enemy.HitPoints);
        Assert.Equal(0, session.Score);

        enemy.TakeDamage(1);
        for (var i = 0; i < 120 && !enemy.IsDead; i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        Assert.True(enemy.IsDead);
        Assert.Equal(1, session.Score);
    }

    [Fact]
    public void Session_rates_can_be_changed_mid_game()
    {
        var session = new GameSession(Center, new Random(3));
        session.Spawner.SetSpawnsPerMinute(EnemyRoster.Gemini, 600f);

        session.Update(Vector2.Zero, null, 0.5f, Width, Height);

        Assert.Equal(5, session.Enemies.Count);
    }

    [Fact]
    public void Initials_accept_up_to_three_letters_and_backspace()
    {
        var entry = new InitialsEntry();
        Assert.False(entry.CanSubmit);

        entry.Type('j');
        entry.Type('1');
        entry.Type('S');
        entry.Type('X');
        entry.Type('Y');
        Assert.Equal("JSX", entry.Text);
        Assert.True(entry.CanSubmit);

        entry.Backspace();
        Assert.Equal("JS", entry.Text);
    }

    [Fact]
    public void High_scores_are_sorted_with_ties_below_and_capped_at_ten()
    {
        var table = new HighScoreTable();

        Assert.Equal(0, table.Add("AAA", 5));
        Assert.Equal(0, table.Add("BBB", 9));
        Assert.Equal(2, table.Add("CCC", 5));
        Assert.Equal(["BBB", "AAA", "CCC"], table.Entries.Select(e => e.Initials));

        for (var i = 0; i < 7; i++)
            table.Add("ZZ", 1);
        Assert.Equal(10, table.Entries.Count);
        Assert.Null(table.Add("LOW", 1));
        Assert.Equal(3, table.Add("TOP", 2));
        Assert.Equal(10, table.Entries.Count);
        Assert.Throws<ArgumentException>(() => table.Add("abcd", 3));
        Assert.Throws<ArgumentException>(() => table.Add("", 3));
    }
}
