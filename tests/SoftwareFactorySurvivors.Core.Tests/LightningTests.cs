using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>The lightning weapon in play: nearest strike, random chain at x0.8 per hop, and no target.</summary>
public class LightningTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private const float Frame = 1f / 60f;
    private static readonly Vector2 Center = new(500, 400);

    [Fact]
    public void Strike_hits_the_nearest_enemy_for_0_8_then_hops_for_0_64()
    {
        var session = LightningSession();
        var near = Target(Center + new Vector2(0, -150));
        var hop = Target(Center + new Vector2(100, -150));
        session.AddEnemy(hop);
        session.AddEnemy(near);

        var segments = RunUntilStrike(session);

        Assert.Equal(10f - 0.8f, near.HitPoints, precision: 4);
        Assert.Equal(10f - 0.64f, hop.HitPoints, precision: 4);
        Assert.Equal(
            [new LightningSegment(session.Ship.Position, near.Position), new LightningSegment(near.Position, hop.Position)],
            segments);
    }

    [Fact]
    public void Three_chains_between_two_enemies_alternate_with_each_hop_at_0_8_of_the_last()
    {
        var session = LightningSession();
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), session.Stats);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), session.Stats);
        Assert.Equal(3, session.Stats.Find(WeaponKind.Lightning)!.Count);
        var a = Target(Center + new Vector2(0, -150));
        var b = Target(Center + new Vector2(100, -150));
        session.AddEnemy(a);
        session.AddEnemy(b);

        var segments = RunUntilStrike(session);

        // A, B, A, B: 0.8, 0.64, 0.512, 0.4096.
        Assert.Equal(10f - 0.8f - 0.512f, a.HitPoints, precision: 4);
        Assert.Equal(10f - 0.64f - 0.4096f, b.HitPoints, precision: 4);
        Assert.Equal(
            [session.Ship.Position, a.Position, b.Position, a.Position, b.Position],
            segments.Select(s => s.From).Append(segments[^1].To));
    }

    [Fact]
    public void Each_hop_picks_at_random_among_enemies_in_range_never_the_one_just_struck()
    {
        var a = Target(new Vector2(0, 0));
        var b = Target(new Vector2(100, 0));
        var c = Target(new Vector2(0, 100));
        var far = Target(new Vector2(-400, 0));
        var enemies = new List<Enemy> { b, c, far, a };
        var firstHops = new HashSet<Enemy>();
        for (var seed = 0; seed < 40; seed++)
        {
            var struck = new List<Enemy>();
            Lightning.Strike(new Vector2(0, -10), 0f, 6, enemies, new Random(seed), (enemy, _) => struck.Add(enemy), []);

            Assert.Equal(7, struck.Count);
            Assert.Same(a, struck[0]);
            Assert.DoesNotContain(far, struck);
            for (var i = 1; i < struck.Count; i++)
                Assert.NotSame(struck[i - 1], struck[i]);
            firstHops.Add(struck[1]);
        }
        Assert.True(firstHops.SetEquals([b, c]));
    }

    [Fact]
    public void Lightning_options_are_exactly_damage_attack_speed_and_chain()
    {
        Assert.Equal(
            [WeaponOption.LightningDamage, WeaponOption.LightningAttackSpeed, WeaponOption.LightningChain],
            Upgrades.OptionsFor(WeaponKind.Lightning));
        Assert.Equal(
            ["+0.4 DAMAGE", "+0.2 ATTACKS/S", "+1 CHAIN"],
            Upgrades.OptionsFor(WeaponKind.Lightning).Select(Upgrades.Effect));
        var lightning = new OwnedWeapon(WeaponKind.Lightning);
        Assert.Equal((0.8f, 1.2f, 1), (lightning.Damage, lightning.AttacksPerSecond, lightning.Count));
    }

    [Fact]
    public void Chain_stops_when_no_other_enemy_is_within_150_px()
    {
        var session = LightningSession();
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), session.Stats);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), session.Stats);
        var lone = Target(Center + new Vector2(0, -150));
        // 151 px from the lone enemy: out of chain range.
        var outOfRange = Target(Center + new Vector2(151, -150));
        session.AddEnemy(lone);
        session.AddEnemy(outOfRange);

        var segments = RunUntilStrike(session);

        Assert.Single(segments);
        Assert.Equal(new LightningSegment(session.Ship.Position, lone.Position), segments[0]);
        Assert.Equal(10f - 0.8f, lone.HitPoints, precision: 4);
        Assert.Equal(10f, outOfRange.HitPoints);
    }

    [Fact]
    public void Strike_with_no_live_enemy_leaves_segments_empty_and_damages_nobody()
    {
        var deadEnemy = Target(new Vector2(100, 100));
        deadEnemy.TakeDamage(100f);
        Assert.True(deadEnemy.IsDead);

        var segments = new List<LightningSegment>();
        var damaged = new List<(Enemy, float)>();
        Lightning.Strike(
            origin: Vector2.Zero,
            damage: 10f,
            hops: 3,
            enemies: [deadEnemy],
            random: new Random(42),
            dealDamage: (enemy, dmg) => damaged.Add((enemy, dmg)),
            segments: segments);

        Assert.Empty(segments);
        Assert.Empty(damaged);

        var session = LightningSession();
        var sessionDead = Target(Center + new Vector2(0, -150));
        sessionDead.TakeDamage(100f);
        session.AddEnemy(sessionDead);

        for (var i = 0; i < 120; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Empty(session.LightningSegments);
    }

    [Fact]
    public void When_chain_runs_out_of_range_reports_only_hops_made_with_endpoints_at_ship_or_struck_enemies()
    {
        var origin = new Vector2(0, 0);
        var first = Target(new Vector2(0, 50), hitPoints: 1f);
        var second = Target(new Vector2(0, 150), hitPoints: 100f); // 100 px from first: within ChainRange (150 px)
        var third = Target(new Vector2(0, 350), hitPoints: 100f);  // 200 px from second: out of ChainRange
        var enemies = new List<Enemy> { first, second, third };

        var segments = new List<LightningSegment>();
        var struck = new List<Enemy>();
        // 5 hops allowed, but only 1 hop can be made before chain runs out of range (first is dead, third is 200 px away)
        Lightning.Strike(
            origin: origin,
            damage: 10f,
            hops: 5,
            enemies: enemies,
            random: new Random(1),
            dealDamage: (e, dmg) =>
            {
                struck.Add(e);
                e.TakeDamage(dmg);
            },
            segments: segments);

        // 1 initial hit + 1 hop = 2 segments
        Assert.Equal(2, segments.Count);
        Assert.Equal([first, second], struck);
        Assert.True(first.IsDead);
        Assert.False(second.IsDead);
        Assert.Equal(new LightningSegment(origin, first.Position), segments[0]);
        Assert.Equal(new LightningSegment(first.Position, second.Position), segments[1]);
        Assert.Equal(100f, third.HitPoints);

        var session = LightningSession();
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), session.Stats);
        Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), session.Stats);
        // Stats has lightning count = 3 (first hit + 2 hops allowed)
        // Make sFirst have very low HP (0.1f) so the first hit (0.8f) kills it.
        var sFirst = Target(Center + new Vector2(0, -100), hitPoints: 0.1f);
        var sSecond = Target(Center + new Vector2(0, -200), hitPoints: 10f);
        var sFar = Target(Center + new Vector2(0, -400), hitPoints: 10f);
        session.AddEnemy(sFirst);
        session.AddEnemy(sSecond);
        session.AddEnemy(sFar);

        var sessionSegments = RunUntilStrike(session);
        // Only 1 hop made because sFirst died and sFar is out of range (>150 px)
        Assert.Equal(2, sessionSegments.Count);
        Assert.Equal(new LightningSegment(session.Ship.Position, sFirst.Position), sessionSegments[0]);
        Assert.Equal(new LightningSegment(sFirst.Position, sSecond.Position), sessionSegments[1]);
        Assert.True(sFirst.IsDead);
        Assert.False(sSecond.IsDead);
        Assert.Equal(10f, sFar.HitPoints);
    }

    [Fact]
    public void With_no_enemy_lightning_keeps_its_shot_and_strikes_the_next_enemy_on_the_next_frame()
    {
        var session = LightningSession();
        var lightning = session.Stats.Find(WeaponKind.Lightning)!;

        for (var i = 0; i < 5 * 60; i++)
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            Assert.Empty(session.LightningSegments);
            Assert.Empty(session.Lasers);
        }
        Assert.Equal(1d, lightning.TimerProgress);

        var late = Target(Center + new Vector2(0, -200));
        session.AddEnemy(late);
        session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Single(session.LightningSegments);
        Assert.Equal(10f - 0.8f, late.HitPoints, precision: 4);
        // The waiting shot was spent; the timer runs on from there, not from a saved-up backlog.
        Assert.InRange(lightning.TimerProgress, 0d, 0.1d);
        session.Update(Vector2.Zero, null, Frame, Width, Height);
        Assert.Empty(session.LightningSegments);
    }

    [Fact]
    public void A_level_20_strike_deals_only_its_hit_and_the_struck_enemy_takes_no_damage_over_the_next_6_s()
    {
        var session = LightningSession();
        for (var i = 1; i < OwnedWeapon.MaxLevel; i++)
            Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningDamage), session.Stats);
        Assert.Equal(20, session.Stats.Find(WeaponKind.Lightning)!.Level);
        var victim = Target(Center + new Vector2(0, -150), hitPoints: 100f);
        session.AddEnemy(victim);

        RunUntilStrike(session);
        var afterStrike = victim.HitPoints;
        Assert.True(afterStrike < 100f);

        // With no further strikes, six seconds of the enemy's own updates take nothing more.
        for (var i = 0; i < 6 * 60; i++)
            victim.Update(victim.Position, Frame);

        Assert.Equal(afterStrike, victim.HitPoints);
    }

    internal static GameSession LightningSession() => KillCreditTests.SessionWith(WeaponKind.Lightning);

    private static Enemy Target(Vector2 position, float hitPoints = 10f) =>
        TestEnemies.Stationary(EnemyRoster.HuggingFace with { HitPoints = hitPoints, Experience = 1 }, position);

    /// <summary>Plays frame by frame until the lightning strikes, and returns that frame's segments.</summary>
    private static List<LightningSegment> RunUntilStrike(GameSession session)
    {
        for (var i = 0; i < 120; i++)
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            if (session.LightningSegments.Count > 0)
                return [.. session.LightningSegments];
        }
        throw new Xunit.Sdk.XunitException("Lightning never struck.");
    }
}
