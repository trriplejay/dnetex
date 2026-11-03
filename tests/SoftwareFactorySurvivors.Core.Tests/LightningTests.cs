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
            Lightning.Strike(new Vector2(0, -10), 0f, 6, 1, enemies, new Random(seed), (enemy, _) => struck.Add(enemy), []);

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
        Assert.Equal(10f - 0.8f, lone.HitPoints, precision: 4);
        Assert.Equal(10f, outOfRange.HitPoints);
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
    public void Level_5_strike_burns_and_the_burn_kill_scores_and_grants_experience()
    {
        var session = LightningSession();
        for (var i = 0; i < 4; i++)
            Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), session.Stats);
        Assert.Equal(5, session.Stats.Find(WeaponKind.Lightning)!.Level);
        var victim = Target(Center + new Vector2(0, -150), hitPoints: 1.5f);
        session.AddEnemy(victim);

        RunUntilStrike(session);
        Assert.True(victim.IsBurning);
        Assert.Equal(0, session.Score);

        // 0.7 HP left burns away at 1/s, before the next strike is due (1/1.2 s after the first).
        for (var i = 0; i < 45 && !victim.IsDead; i++)
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            Assert.Empty(session.LightningSegments);
        }

        Assert.True(victim.IsDead);
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(victim.Experience, session.Leveling.Experience);
        Assert.DoesNotContain(victim, session.Enemies);

        // Credited once: later frames add nothing.
        session.Update(Vector2.Zero, null, 2f, Width, Height);
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(victim.Experience, session.Leveling.Experience);
    }

    [Fact]
    public void Below_level_5_a_strike_does_not_burn()
    {
        var session = LightningSession();
        for (var i = 0; i < 3; i++)
            Upgrades.Apply(Upgrade.ForWeapon(WeaponKind.Lightning, WeaponOption.LightningChain), session.Stats);
        var victim = Target(Center + new Vector2(0, -150), hitPoints: 1.5f);
        session.AddEnemy(victim);

        RunUntilStrike(session);
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);

        Assert.False(victim.IsBurning);
        Assert.Equal(0.7f, victim.HitPoints, precision: 4);
        Assert.Equal(0, session.Score);
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
