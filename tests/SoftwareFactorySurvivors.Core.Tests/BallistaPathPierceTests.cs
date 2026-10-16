using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class BallistaPathPierceTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void Applying_Ballista_upgrade_leaves_stats_pierce_unchanged()
    {
        var stats = new PlayerStats();
        var initialPierce = stats.Pierce;

        Upgrades.Apply(Upgrade.Ballista, stats);

        Assert.Equal(WeaponPath.Ballista, stats.WeaponPath);
        Assert.Equal(initialPierce, stats.Pierce);
    }

    [Fact]
    public void Applying_Piercing_upgrade_adds_exactly_one_to_stats_pierce()
    {
        var stats = new PlayerStats();
        var initialPierce = stats.Pierce;

        Upgrades.Apply(Upgrade.Piercing, stats);

        Assert.Equal(initialPierce + 1, stats.Pierce);
    }

    [Fact]
    public void Bola_fired_at_stats_pierce_zero_damages_exactly_one_of_several_lined_up_enemies()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.WeaponPath = WeaponPath.Bola;
        session.Stats.Pierce = 0;
        session.Stats.Damage = 2f;

        var tough = EnemyRoster.Gemini with { HitPoints = 10f };
        var nose = session.Ship.Nose;
        var column = Enumerable.Range(0, 4)
            .Select(i => TestEnemies.Stationary(tough, nose - new Vector2(0, 100 + 80 * i)))
            .ToArray();

        foreach (var enemy in column)
            session.AddEnemy(enemy);

        // Advance until the first volley at 1.0s fires and traverses the column, then finishes
        for (var i = 0; i < 110; i++)
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);

        var damagedCount = column.Count(e => e.HitPoints < 10f);
        Assert.Equal(1, damagedCount);
        Assert.Empty(session.Lasers);
    }
}
