using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>The session reports where level-5 laser impacts exploded, for the screen to draw.</summary>
public class LaserExplosionEventTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private const float Frame = 1f / 60f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void A_level_5_laser_impact_reports_its_point_even_with_no_enemy_to_splash_then_clears()
    {
        var session = KillCreditTests.SessionWith(WeaponKind.Laser);
        Raise(session, 4);
        var target = Target();
        session.AddEnemy(target);

        var impact = RunUntilImpact(session);

        Assert.Equal([target.Position], impact);
        session.Update(Vector2.Zero, null, Frame, Width, Height);
        Assert.Empty(session.LaserExplosions);
    }

    [Fact]
    public void Below_level_5_a_laser_impact_reports_no_explosion()
    {
        var session = KillCreditTests.SessionWith(WeaponKind.Laser);
        Raise(session, 3);
        var target = Target();
        session.AddEnemy(target);

        for (var i = 0; i < 120; i++)
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            Assert.Empty(session.LaserExplosions);
        }

        Assert.True(target.HitPoints < 1000f);
    }

    private static void Raise(GameSession session, int times)
    {
        for (var i = 0; i < times; i++)
            Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Laser, WeaponOption.LaserAttackSpeed), session.Stats);
    }

    private static Enemy Target() =>
        TestEnemies.Stationary(EnemyRoster.HuggingFace with { HitPoints = 1000f, Experience = 1 }, Center + new Vector2(0, -150));

    private static List<Vector2> RunUntilImpact(GameSession session)
    {
        for (var i = 0; i < 120; i++)
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            if (session.LaserExplosions.Count > 0)
                return [.. session.LaserExplosions];
        }
        throw new Xunit.Sdk.XunitException("The laser never exploded.");
    }
}
