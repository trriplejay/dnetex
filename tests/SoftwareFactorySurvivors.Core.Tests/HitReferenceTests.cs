using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>
/// Reference tests pinning how projectiles hit enemies and expire, end to end through GameSession.Update and
/// ProjectileShape.Touches, so performance work on the hit pass can't change what the player sees.
/// Every scenario fires its volley with a one-second first frame: the ship starts at Center facing up, and the
/// volley spawns at its nose (Center - (0, 24)) without moving in the frame it was fired.
/// </summary>
public class HitReferenceTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);
    private static readonly Vector2 Nose = Center - new Vector2(0, 24);

    // ---- Enclosure and grazes ----

    [Fact]
    public void Ballista_arrowhead_wholly_enclosing_an_enemy_hits_it()
    {
        // The head spans y 312..324 above the nose's x, 3 pixels either side at y 318; the shaft's capsule
        // stops at y 322, so the speck touches nothing but lies inside the triangle.
        var speck = TestEnemies.Stationary(Speck(0.3f), new Vector2(Nose.X, Nose.Y - 58f));
        var ballista = new Ballista(Nose, 0f);
        var strokes = speck.GetStrokes();
        var shaft = ballista.GetShaft();
        Assert.False(Collision.ThickSegmentTouches(shaft[0], shaft[1], ballista.ShaftWidth / 2f, strokes));
        var head = ballista.GetArrowhead();
        for (var i = 0; i < head.Length - 1; i++)
            Assert.False(Collision.SegmentsIntersect(head[i], head[i + 1], strokes[0][0], strokes[0][1]));
        Assert.True(ballista.GetShape().Touches(strokes));

        FireOnce(WeaponPath.Ballista, speck);

        Assert.Equal(2f, speck.HitPoints);
    }

    [Theory]
    [InlineData(1.9f, true)]
    [InlineData(2.1f, false)]
    public void Laser_edge_graze_within_its_half_width_hits(float gap, bool hit)
    {
        // The laser runs from the nose straight up for 13 pixels with half-width 2; the speck's nearest end is
        // `gap` pixels to its right.
        var speck = TestEnemies.Stationary(Speck(0.5f), new Vector2(Nose.X + gap + 0.5f, Nose.Y - 6f));

        FireOnce(WeaponPath.None, speck);

        Assert.Equal(hit ? 2f : 3f, speck.HitPoints);
    }

    [Theory]
    [InlineData(5.8f, true)]
    [InlineData(6.2f, false)]
    public void Bola_ball_graze_within_its_radius_hits(float below, bool hit)
    {
        // A fresh bola fired straight up lies across the flight path: ball B (radius 6) sits 26 right of the nose.
        // `below` pixels under its centre is beyond the line's 2-pixel half-width.
        var ballB = Nose + new Vector2(26f, 0);
        var speck = TestEnemies.Stationary(Speck(0.2f), ballB + new Vector2(0, below));

        FireOnce(WeaponPath.Bola, speck);

        Assert.Equal(hit ? 2f : 3f, speck.HitPoints);
    }

    [Fact]
    public void Bola_ball_radius_and_span_match_scale()
    {
        var small = new Bola(Nose, 0f, scale: 1f);
        var large = new Bola(Nose, 0f, scale: 2f);

        Assert.Equal(6f, small.BallRadius);
        Assert.Equal(12f, large.BallRadius);
        Assert.Equal(52f, small.Span);
        Assert.Equal(104f, large.Span);
        Assert.Equal(52f, Vector2.Distance(small.BallA, small.BallB), precision: 3);
        Assert.Equal(104f, Vector2.Distance(large.BallA, large.BallB), precision: 3);
    }

    // ---- Who takes part in the pass ----

    [Fact]
    public void Shield_spawned_by_a_phase_change_mid_pass_is_not_hit_in_that_pass()
    {
        var (session, boss) = PhaseBossScenario();
        var shield = Assert.Single(boss.Shields);
        // The fresh shield sits right in both lasers' paths and both lasers have pierce left after hitting the
        // boss, yet neither hits it: it only joins play after the hit pass.
        Assert.Equal(2, session.Lasers.Count);
        Assert.Equal(shield.Type.HitPoints, shield.HitPoints);
        Assert.True(session.Lasers.All(laser => laser.Touches(shield.GetStrokes())));
        Assert.Contains(shield, session.Enemies);
    }

    [Fact]
    public void Enemy_pushed_into_its_next_phase_mid_pass_is_tested_by_later_projectiles_in_its_new_phase()
    {
        var (session, boss) = PhaseBossScenario();

        // The first laser drops it to 3 of 4, which starts the shielded phase; the second laser still hits it
        // (spending its pierce) but the new shield blocks the damage.
        Assert.Equal(1, boss.PhasesReached);
        Assert.Equal(3f, boss.HitPoints);
        Assert.True(boss.IsShielded);
        Assert.All(session.Lasers, laser => Assert.Equal(0, laser.PierceRemaining));
    }

    [Fact]
    public void Shields_collected_after_the_hit_pass_bring_their_own_shields_only_on_the_next_frame()
    {
        // The phase shield's own orbiters are still waiting when the shield joins play, because the pass that
        // collects new shields walks the enemies as they were before it began.
        var tiny = Speck(0.2f) with { HitPoints = 1f };
        var inner = Speck(0.2f) with { Shield = new EnemyShield(tiny, 2, 10f, 1f) };
        var bossType = Wide(30f) with
        {
            HitPoints = 4f,
            Phases = [new BossPhase(0.75f, Shield: new EnemyShield(inner, 2, 200f, 1f))],
        };
        var session = QuietSession(WeaponPath.None);
        var boss = TestEnemies.Stationary(bossType, Nose - new Vector2(0, 6));
        session.AddEnemy(boss);

        session.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(1 + 2, session.Enemies.Count);

        session.Update(Vector2.Zero, null, 0.001f, Width, Height);
        Assert.Equal(1 + 2 + 4, session.Enemies.Count);
    }

    [Fact]
    public void Enemy_created_by_an_ability_does_not_use_its_own_ability_in_the_same_pass()
    {
        var calls = new List<int>();
        var parent = TestEnemies.Stationary(Breeder(0, calls), new Vector2(100, 100));
        var session = QuietSession(WeaponPath.None);
        session.AddEnemy(parent);

        session.Update(Vector2.Zero, null, 0.01f, Width, Height);

        Assert.Equal([0], calls);
        Assert.Equal(2, session.Enemies.Count);

        session.Update(Vector2.Zero, null, 0.01f, Width, Height);
        Assert.Equal([0, 0, 1], calls);
        Assert.Equal(3, session.Enemies.Count);
    }

    [Fact]
    public void Enemy_thrown_before_the_hit_pass_can_be_hit_in_that_same_frame()
    {
        // Abilities run before the hit pass, so a minion thrown into the laser's path this frame is in play for it.
        var minion = Speck(0.5f);
        var thrower = TestEnemies.Stationary(
            Speck(0.2f) with { CreateAbility = () => new DropOnce(minion, Nose - new Vector2(0, 6)) },
            new Vector2(100, 100));
        var session = QuietSession(WeaponPath.None);
        session.AddEnemy(thrower);

        session.Update(Vector2.Zero, null, 1f, Width, Height);

        var thrown = Assert.Single(session.Enemies, enemy => enemy != thrower);
        Assert.Equal(2f, thrown.HitPoints);
    }

    [Fact]
    public void Each_projectile_walks_the_enemy_list_from_the_start_before_the_next_projectile_goes()
    {
        // Two lasers, two tough enemies both lasers cross. Projectiles-first: laser 1 takes enemy 1 and is used
        // up, then laser 2 starts again from enemy 1 and takes it too, leaving enemy 2 untouched.
        var session = QuietSession(WeaponPath.None);
        session.Stats.ProjectileCount = 2;
        var first = TestEnemies.Stationary(Wide(30f), Nose - new Vector2(0, 6));
        var second = TestEnemies.Stationary(Wide(30f), Nose - new Vector2(0, 8));
        session.AddEnemy(first);
        session.AddEnemy(second);

        session.Update(Vector2.Zero, null, 1f, Width, Height);

        Assert.Equal(1f, first.HitPoints);
        Assert.Equal(3f, second.HitPoints);
        Assert.Empty(session.Lasers);
    }

    [Fact]
    public void Projectiles_resolve_against_enemies_in_list_order_projectiles_first()
    {
        // Two lasers, two enemies both lasers cross; the first enemy dies to one hit. Laser 1 takes the earliest
        // enemy in the list and kills it; laser 2 then sees it dead, skips it and takes the second enemy.
        var session = QuietSession(WeaponPath.None);
        session.Stats.ProjectileCount = 2;
        var first = TestEnemies.Stationary(Wide(30f) with { HitPoints = 1f }, Nose - new Vector2(0, 6));
        var second = TestEnemies.Stationary(Wide(30f), Nose - new Vector2(0, 8));
        session.AddEnemy(first);
        session.AddEnemy(second);

        session.Update(Vector2.Zero, null, 1f, Width, Height);

        Assert.True(first.IsDead);
        Assert.Equal(2f, second.HitPoints);
        Assert.Equal(1, session.Score);
        Assert.Equal([second], session.Enemies);
        Assert.Empty(session.Lasers);
    }

    // ---- Expiry ----

    [Fact]
    public void Projectile_leaving_the_screen_in_the_frame_it_hits_still_registers_the_hit()
    {
        // Ship near the top edge: its nose is at y = -4, so the laser (-4 up to -17) is wholly off screen the
        // moment it spawns. With pierce it isn't used up by the hit; expiry alone removes it.
        var session = new GameSession(new Vector2(500, 20), new Random(1), roster: [], bosses: [], rings: []);
        session.Stats.Pierce = 5;
        var target = TestEnemies.Stationary(Speck(5f), new Vector2(500, -10));
        session.AddEnemy(target);

        session.Update(Vector2.Zero, null, 1f, Width, Height);

        Assert.Equal(2f, target.HitPoints);
        Assert.Empty(session.Lasers);
    }

    [Fact]
    public void Expiry_fires_on_the_frame_the_exact_screen_overlap_test_says_it_is_gone()
    {
        // Nose at y = 26; the laser climbs 6 pixels per 0.01 s frame and is gone once its tail passes y = 0.
        var session = new GameSession(new Vector2(50, 50), new Random(1), roster: [], bosses: [], rings: []);
        session.Update(Vector2.Zero, null, 1f, Width, Height);
        var laser = Assert.Single(session.Lasers);

        for (var frame = 1; frame <= 4; frame++)
        {
            session.Update(Vector2.Zero, null, 0.01f, Width, Height);
            Assert.Same(laser, Assert.Single(session.Lasers));
            Assert.True(laser.GetShape().Overlaps(Width, Height));
        }

        session.Update(Vector2.Zero, null, 0.01f, Width, Height);
        Assert.Empty(session.Lasers);
        Assert.False(laser.GetShape().Overlaps(Width, Height));
    }

    // ---- Allocation ----

    [Fact]
    public void Steady_state_frame_allocates_no_copy_of_the_enemy_list()
    {
        // One contact hit makes the ship briefly immune, so the contact pass (which builds outlines) is skipped
        // and what's left of the frame shouldn't grow with the enemy count.
        var session = QuietSession(WeaponPath.None);
        session.AddEnemy(TestEnemies.Stationary(Speck(5f), Center));
        session.Update(Vector2.Zero, null, 0.001f, Width, Height);
        Assert.True(session.InvulnerableSecondsLeft > 0f);

        const int count = 2_000;
        for (var i = 0; i < count; i++)
            session.AddEnemy(TestEnemies.Stationary(Speck(0.5f), new Vector2(100 + i % 800, 700 + i / 800 * 10)));
        session.Update(Vector2.Zero, null, 0.001f, Width, Height);

        var before = GC.GetAllocatedBytesForCurrentThread();
        session.Update(Vector2.Zero, null, 0.001f, Width, Height);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(count, session.Enemies.Count);
        // A single Enemy[2000] copy is over 16 KB; the frame must stay well below one.
        Assert.True(allocated < count * IntPtr.Size / 2, $"Frame allocated {allocated} bytes");
    }

    // ---- Helpers ----

    private static GameSession QuietSession(WeaponPath path)
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.WeaponPath = path;
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 1_000;
        return session;
    }

    private static GameSession FireOnce(WeaponPath path, Enemy enemy)
    {
        var session = QuietSession(path);
        session.AddEnemy(enemy);
        session.Update(Vector2.Zero, null, 1f, Width, Height);
        return session;
    }

    // Boss of 4 hit points crossed by both lasers of a two-laser volley; at 3 its phase spawns a shield
    // right beside it, also across the lasers' path.
    private static (GameSession Session, Enemy Boss) PhaseBossScenario()
    {
        var bossType = Wide(30f) with
        {
            HitPoints = 4f,
            Phases = [new BossPhase(0.75f, Shield: new EnemyShield(Wide(30f), 1, 1f, 1f))],
        };
        var session = QuietSession(WeaponPath.None);
        session.Stats.ProjectileCount = 2;
        session.Stats.Pierce = 1;
        var boss = TestEnemies.Stationary(bossType, Nose - new Vector2(0, 6));
        session.AddEnemy(boss);

        session.Update(Vector2.Zero, null, 1f, Width, Height);
        return (session, boss);
    }

    /// <summary>A Gemini (3 hit points) whose outline is one short horizontal segment of the given half-length.</summary>
    private static EnemyType Speck(float halfLength) =>
        EnemyRoster.Gemini with { Outline = [[new(-halfLength, 0), new(halfLength, 0)]] };

    private static EnemyType Wide(float halfLength) => Speck(halfLength);

    private static EnemyType Breeder(int generation, List<int> calls) =>
        Speck(0.2f) with { CreateAbility = () => new Breed(generation, calls) };

    /// <summary>Records each call; the first call creates one child that breeds the same way, up to generation 3.</summary>
    private sealed class Breed(int generation, List<int> calls) : IEnemyAbility
    {
        private bool _bred;

        public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
        {
            calls.Add(generation);
            if (_bred || generation >= 3)
                return [];
            _bred = true;
            return [TestEnemies.Stationary(Breeder(generation + 1, calls), owner.Position + new Vector2(5, 0))];
        }
    }

    /// <summary>Drops one stationary enemy at a fixed spot on its first call.</summary>
    private sealed class DropOnce(EnemyType type, Vector2 spot) : IEnemyAbility
    {
        private bool _dropped;

        public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
        {
            if (_dropped)
                return [];
            _dropped = true;
            return [TestEnemies.Stationary(type, spot)];
        }
    }
}
