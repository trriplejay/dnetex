using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class EnemySpawnerTests
{
    private const float Width = 1000f;
    private const float Height = 800f;

    private static readonly EnemyType Square = EnemyRoster.Gemini with { Name = "Square", Radius = 10f, DefaultSpawnsPerMinute = 60f };

    [Fact]
    public void Gemini_spawns_every_two_seconds_by_default_just_off_screen()
    {
        var spawner = new EnemySpawner(new Random(7));
        spawner.Add(EnemyRoster.Gemini);
        Assert.Equal(30f, spawner.GetSpawnsPerMinute(EnemyRoster.Gemini));

        Assert.Empty(spawner.Update(1.5f, Width, Height));
        var spawned = spawner.Update(0.5f, Width, Height).Concat(spawner.Update(6f, Width, Height)).ToArray();

        Assert.Equal(4, spawned.Length);
        Assert.All(spawned, enemy =>
        {
            var p = enemy.Position;
            Assert.True(p.X < 0 || p.X > Width || p.Y < 0 || p.Y > Height, $"{p} should be off screen");
            Assert.InRange(p.X, -EnemyRoster.Gemini.Radius, Width + EnemyRoster.Gemini.Radius);
            Assert.InRange(p.Y, -EnemyRoster.Gemini.Radius, Height + EnemyRoster.Gemini.Radius);
        });
    }

    [Fact]
    public void Each_type_spawns_at_its_own_rate()
    {
        var spawner = new EnemySpawner(new Random(1));
        spawner.Add(EnemyRoster.Gemini);
        spawner.Add(Square);

        var spawned = spawner.Update(4f, Width, Height);

        Assert.Equal(2 + 4, spawned.Count);
    }

    [Fact]
    public void Rate_change_applies_from_the_next_update_without_losing_progress()
    {
        var spawner = new EnemySpawner(new Random(1));
        spawner.Add(EnemyRoster.Gemini);

        Assert.Empty(spawner.Update(1f, Width, Height)); // half way to the next spawn at 30/min
        spawner.SetSpawnsPerMinute(EnemyRoster.Gemini, 120f);

        Assert.Equal(120f, spawner.GetSpawnsPerMinute(EnemyRoster.Gemini));
        Assert.Single(spawner.Update(0.25f, Width, Height)); // 0.5 + 0.5 = one spawn
        Assert.Equal(4, spawner.Update(2f, Width, Height).Count);
    }

    [Fact]
    public void Zero_rate_pauses_a_type_and_bad_rates_are_rejected()
    {
        var spawner = new EnemySpawner(new Random(1));
        spawner.Add(EnemyRoster.Gemini);
        spawner.SetSpawnsPerMinute(EnemyRoster.Gemini, 0f);

        Assert.Empty(spawner.Update(60f, Width, Height));
        Assert.Throws<ArgumentOutOfRangeException>(() => spawner.SetSpawnsPerMinute(EnemyRoster.Gemini, -1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => spawner.SetSpawnsPerMinute(EnemyRoster.Gemini, float.NaN));
        Assert.Throws<ArgumentException>(() => spawner.SetSpawnsPerMinute(Square, 10f));
    }

    [Fact]
    public void Adding_a_type_twice_keeps_one_entry_and_its_rate()
    {
        var spawner = new EnemySpawner(new Random(1));
        spawner.Add(EnemyRoster.Gemini);
        spawner.SetSpawnsPerMinute(EnemyRoster.Gemini, 5f);

        spawner.Add(EnemyRoster.Gemini);

        Assert.Single(spawner.Types);
        Assert.Equal(5f, spawner.GetSpawnsPerMinute(EnemyRoster.Gemini));
    }
}
