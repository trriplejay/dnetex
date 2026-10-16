using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class PierceTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void Projectile_ctor_sets_pierce_remaining_and_spends_per_distinct_hit()
    {
        var projectile = new Laser(Vector2.Zero, 0f, scale: 1f, pierce: 2);
        Assert.Equal(2, projectile.PierceRemaining);

        var e1 = TestEnemies.StationaryGemini(new Vector2(0, -10));
        var e2 = TestEnemies.StationaryGemini(new Vector2(0, -20));
        var e3 = TestEnemies.StationaryGemini(new Vector2(0, -30));

        Assert.False(projectile.HasHit(e1));
        // Hit 1: spends 1 pierce, not used up
        Assert.False(projectile.RegisterHit(e1));
        Assert.True(projectile.HasHit(e1));
        Assert.Equal(1, projectile.PierceRemaining);

        // Hit 2: spends 1 pierce, not used up
        Assert.False(projectile.RegisterHit(e2));
        Assert.True(projectile.HasHit(e2));
        Assert.Equal(0, projectile.PierceRemaining);

        // Hit 3 (1 + pierce = 3): pierce exhausted, projectile is used up
        Assert.True(projectile.RegisterHit(e3));
        Assert.True(projectile.HasHit(e3));
        Assert.Equal(0, projectile.PierceRemaining);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public void Projectile_with_pierce_damages_at_most_p_plus_1_of_4_lined_up_enemies(int pierce, int expectedDamaged)
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.Pierce = pierce;
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
        Assert.Equal(expectedDamaged, damagedCount);
        Assert.Empty(session.Lasers);
    }
}
