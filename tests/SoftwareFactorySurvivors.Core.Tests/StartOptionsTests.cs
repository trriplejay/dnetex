using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class StartOptionsTests
{
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void No_arguments_start_a_normal_game()
    {
        Assert.Equal(new StartOptions(0f, 1), StartOptions.Parse([]));
    }

    [Theory]
    [InlineData(new[] { "--time", "2:00", "--level", "5" }, 120f, 5)]
    [InlineData(new[] { "--level=3", "--time=1:30" }, 90f, 3)]
    [InlineData(new[] { "--time", "150" }, 150f, 1)]
    [InlineData(new[] { "--time", "09:59" }, 599f, 1)]
    public void Reads_time_and_level(string[] args, float seconds, int level)
    {
        Assert.Equal(new StartOptions(seconds, level), StartOptions.Parse(args));
    }

    [Theory]
    [InlineData("--speed", "2")]
    [InlineData("--time", "10:00")]
    [InlineData("--time", "1:75")]
    [InlineData("--time", "abc")]
    [InlineData("--time", "-5")]
    [InlineData("--level", "0")]
    [InlineData("--level", "two")]
    public void Rejects_bad_options(string name, string value)
    {
        Assert.Throws<ArgumentException>(() => StartOptions.Parse([name, value]));
    }

    [Fact]
    public void Rejects_an_option_missing_its_value()
    {
        Assert.Throws<ArgumentException>(() => StartOptions.Parse(["--level"]));
    }

    [Fact]
    public void Starting_late_sets_the_clock_and_brings_in_every_enemy_type_that_has_arrived()
    {
        var session = new GameSession(Center, new Random(1), start: new StartOptions(StartSeconds: 200f));

        Assert.Equal(200f, session.ElapsedSeconds);
        Assert.Equal(EnemyRoster.All.Where(type => type.ArrivesAtSeconds <= 200f), session.Spawner.Types);
    }

    [Fact]
    public void Boss_due_at_the_start_time_enters_on_the_first_frame_but_earlier_bosses_are_skipped()
    {
        var atBoss = new GameSession(Center, new Random(1), start: new StartOptions(StartSeconds: 180f));
        atBoss.Update(Vector2.Zero, null, 1f / 60f, 1000f, 800f);
        Assert.Same(EnemyRoster.GeminiBoss, Assert.Single(atBoss.Bosses).Type);

        var afterBoss = new GameSession(Center, new Random(1), start: new StartOptions(StartSeconds: 181f));
        for (var i = 0; i < 60; i++)
            afterBoss.Update(Vector2.Zero, null, 1f / 60f, 1000f, 800f);
        Assert.False(afterBoss.IsBossActive);
    }

    [Fact]
    public void Starting_at_level_5_offers_four_rounds_of_upgrades_before_play_begins()
    {
        var session = new GameSession(Center, new Random(1), start: new StartOptions(StartLevel: 5));

        Assert.Equal(5, session.Leveling.Level);
        Assert.Equal(0, session.Leveling.Experience);
        Assert.Equal(Leveling.Required(5), session.Leveling.ExperienceToNextLevel);

        for (var round = 0; round < 4; round++)
        {
            Assert.True(session.IsChoosingUpgrade, $"Round {round + 1} wasn't offered");
            session.Update(Vector2.Zero, null, 1f, 1000f, 800f);
            Assert.Equal(0f, session.ElapsedSeconds);
            session.ChooseUpgrade(0);
        }

        Assert.False(session.IsChoosingUpgrade);
        session.Update(Vector2.Zero, null, 1f, 1000f, 800f);
        Assert.Equal(1f, session.ElapsedSeconds);
    }

    [Fact]
    public void Level_1_start_offers_no_upgrades()
    {
        Assert.False(new GameSession(Center, new Random(1)).IsChoosingUpgrade);
    }
}
