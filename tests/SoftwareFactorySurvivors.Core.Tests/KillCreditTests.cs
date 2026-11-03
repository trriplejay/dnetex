using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>
/// Every kind of weapon damage credits a kill's score and experience exactly once. Burn kills are covered by
/// LightningTests.Level_5_strike_burns_and_the_burn_kill_scores_and_grants_experience.
/// </summary>
public class KillCreditTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private const float Frame = 1f / 60f;
    private static readonly Vector2 Center = new(500, 400);

    public enum Source
    {
        LaserHit,
        LaserExplosion,
        LightningStrike,
        LightningHop,
        Orb,
        Ring,
    }

    [Theory]
    [InlineData(Source.LaserHit)]
    [InlineData(Source.LaserExplosion)]
    [InlineData(Source.LightningStrike)]
    [InlineData(Source.LightningHop)]
    [InlineData(Source.Orb)]
    [InlineData(Source.Ring)]
    public void A_one_hp_kill_adds_the_kill_score_and_experience_exactly_once(Source source)
    {
        var (session, victim) = Arrange(source);

        for (var i = 0; i < 3 * 60 && !victim.IsDead; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.True(victim.IsDead);
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(victim.Experience, session.Leveling.Experience);

        // The kill is never credited again, however long the weapon keeps working.
        for (var i = 0; i < 2 * 60; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(victim.Experience, session.Leveling.Experience);
        Assert.Equal(0, session.Leveling.Level - 1);
    }

    [Theory]
    [InlineData(WeaponKind.Laser)]
    [InlineData(WeaponKind.Lightning)]
    [InlineData(WeaponKind.Orbs)]
    public void An_enemy_already_dead_earns_nothing(WeaponKind kind)
    {
        var session = SessionWith(kind);
        var corpse = Victim(VictimSpot(kind));
        corpse.TakeDamage(corpse.HitPoints);
        corpse.ApplyBurn();
        session.AddEnemy(corpse);

        for (var i = 0; i < 2 * 60; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Equal(0, session.Score);
        Assert.Equal(0, session.Leveling.Experience);
        Assert.Empty(session.LightningSegments);
    }

    /// <summary>A quiet session (nothing spawns) whose starting pick was the given weapon.</summary>
    internal static GameSession SessionWith(WeaponKind kind)
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], rings: []);
        session.ChooseUpgrade(session.UpgradeOffer!.ToList().IndexOf(Upgrade.NewWeapon(kind)));
        Assert.False(session.IsChoosingUpgrade);
        return session;
    }

    private static (GameSession Session, Enemy Victim) Arrange(Source source)
    {
        switch (source)
        {
            case Source.LaserHit:
            {
                var session = SessionWith(WeaponKind.Laser);
                return (session, Add(session, Victim(VictimSpot(WeaponKind.Laser))));
            }
            case Source.LaserExplosion:
            {
                var session = SessionWith(WeaponKind.Laser);
                Raise(session, WeaponKind.Laser, WeaponOption.LaserAttackSpeed, 4);
                // The laser flies straight up into a tough enemy; the victim beside it only takes the splash.
                Add(session, Tough(Center + new Vector2(0, -150)));
                return (session, Add(session, Victim(Center + new Vector2(32, -150))));
            }
            case Source.LightningStrike:
            {
                var session = SessionWith(WeaponKind.Lightning);
                return (session, Add(session, Victim(VictimSpot(WeaponKind.Lightning))));
            }
            case Source.LightningHop:
            {
                var session = SessionWith(WeaponKind.Lightning);
                // 0.8 + 2 x 0.4 = 1.6 damage, so the 0.8x hop deals 1.28 and kills a 1-HP enemy.
                Raise(session, WeaponKind.Lightning, WeaponOption.LightningDamage, 2);
                Add(session, Tough(Center + new Vector2(0, -150)));
                return (session, Add(session, Victim(Center + new Vector2(0, -250))));
            }
            case Source.Orb:
            {
                var session = SessionWith(WeaponKind.Orbs);
                return (session, Add(session, Victim(VictimSpot(WeaponKind.Orbs))));
            }
            case Source.Ring:
            {
                var session = SessionWith(WeaponKind.Orbs);
                Raise(session, WeaponKind.Orbs, WeaponOption.OrbsCount, 4);
                Assert.True(session.Stats.Find(WeaponKind.Orbs)!.Orbs!.IsRingMode);
                return (session, Add(session, Victim(VictimSpot(WeaponKind.Orbs))));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(source));
        }
    }

    /// <summary>Where each weapon reaches first: in the laser's path, nearest for lightning, on the orbit for orbs.</summary>
    private static Vector2 VictimSpot(WeaponKind kind) => kind switch
    {
        WeaponKind.Orbs => Center + new Vector2(OrbitingOrbs.DefaultOrbitRadius, 0),
        _ => Center + new Vector2(0, -150),
    };

    private static void Raise(GameSession session, WeaponKind kind, WeaponOption option, int times)
    {
        for (var i = 0; i < times; i++)
            Upgrades.Apply(Upgrade.ForWeapon(kind, option), session.Stats);
    }

    private static Enemy Add(GameSession session, Enemy enemy)
    {
        session.AddEnemy(enemy);
        return enemy;
    }

    private static Enemy Victim(Vector2 position) =>
        TestEnemies.Stationary(EnemyRoster.HuggingFace with { HitPoints = 1f, Experience = 1 }, position);

    private static Enemy Tough(Vector2 position) =>
        TestEnemies.Stationary(EnemyRoster.HuggingFace with { HitPoints = 1000f, Experience = 3 }, position);
}
