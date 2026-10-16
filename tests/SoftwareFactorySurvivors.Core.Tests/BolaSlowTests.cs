using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>
/// Bola hits slow what they hit to a quarter of its movement for 2 seconds, and Ballista bolts pierce two more
/// enemies than the player's piercing. Every hit lands through GameSession.Update: a fresh quiet session fires one
/// volley straight up from the nose in a single 1 ms frame, so the slow's clock barely moves before the hit. Targets
/// hold still while the session runs (so the volley can't miss) and then move with their real movement pattern.
/// </summary>
public class BolaSlowTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private const float Dt = 0.1f;
    private static readonly Vector2 Center = new(500, 400);
    private static readonly Vector2 Nose = Center - new Vector2(0, 24);

    // Far below the screen, so chasing enemies never reach it during a test.
    private static readonly Vector2 Player = new(500, 5000);

    // The bola's tether lies across the nose (half-width 2) the frame it is fired; lasers and ballista bolts run up from it.
    private static readonly Vector2 BolaSpot = Nose - new Vector2(0, 1.5f);
    private static readonly Vector2 ShaftSpot = Nose - new Vector2(0, 6f);

    public static TheoryData<string> Patterns => new() { "chase", "spiral" };

    // ---- 25% of the movement, for any pattern ----

    [Theory]
    [MemberData(nameof(Patterns))]
    public void Bola_hit_makes_an_enemy_move_a_quarter_as_far_as_an_identical_unhit_one(string pattern)
    {
        var slowed = Target(BolaSpot, Pattern(pattern));
        var twin = Target(BolaSpot, Pattern(pattern));
        Hit(WeaponPath.Bola, slowed);
        Assert.Equal(2f, slowed.HitPoints);

        var slowedStep = Step(slowed);
        var twinStep = Step(twin);

        Assert.True(twinStep.Length() > 1f);
        AssertClose(twinStep * 0.25f, slowedStep);
    }

    [Fact]
    public void Second_bola_hit_does_not_compound_the_slow()
    {
        var enemy = Target(BolaSpot, new ChasePlayer(60f));
        Hit(WeaponPath.Bola, enemy);
        Hit(WeaponPath.Bola, enemy);
        Assert.Equal(1f, enemy.HitPoints);

        AssertClose(0.25f * 60f * Dt, Step(enemy).Length());
    }

    // ---- 2 seconds from the last hit ----

    [Fact]
    public void Slow_lasts_two_seconds_of_game_time_then_movement_is_normal()
    {
        var enemy = Target(BolaSpot, new ChasePlayer(60f));
        Hit(WeaponPath.Bola, enemy);

        Advance(enemy, 1.8f);
        // The frame ending at 1.9 s is still slowed.
        AssertClose(0.25f * 60f * Dt, Step(enemy).Length());

        Advance(enemy, 0.11f);
        // The frame starting at 2.01 s is back to normal.
        AssertClose(60f * Dt, Step(enemy).Length());
        Assert.False(enemy.IsSlowed);
        Assert.Equal(0f, enemy.SlowSecondsLeft);
    }

    [Fact]
    public void Re_hit_restarts_the_slow_at_the_full_two_seconds_without_adding_to_it()
    {
        var enemy = Target(BolaSpot, new ChasePlayer(60f));
        Hit(WeaponPath.Bola, enemy);
        Advance(enemy, 1.5f);
        Hit(WeaponPath.Bola, enemy);
        Assert.Equal(1f, enemy.HitPoints);

        // 1.9 s after the first hit: slowed.
        Advance(enemy, 0.3f);
        AssertClose(0.25f * 60f * Dt, Step(enemy).Length());
        // 2.0-2.1 s after the first hit: still slowed, because the second hit restarted the clock.
        AssertClose(0.25f * 60f * Dt, Step(enemy).Length());

        // Just after 2 s from the second hit: normal (an added-on slow would still be running).
        Advance(enemy, 2f - 0.5f + 0.01f);
        AssertClose(60f * Dt, Step(enemy).Length());
    }

    // ---- The slow scales displacement only: a pattern's own ramp keeps real time ----

    [Fact]
    public void Slowed_cursor_moves_a_quarter_as_far_as_its_unhit_twin_on_the_same_frame()
    {
        var slowed = Target(BolaSpot, CursorMovement(), EnemyRoster.Cursor);
        var twin = Target(BolaSpot, CursorMovement(), EnemyRoster.Cursor);
        Hit(WeaponPath.Bola, slowed);
        Assert.True(slowed.IsSlowed);

        // A second into the slow, both have ramped up the same way, so the slowed one covers exactly a quarter.
        for (var i = 0; i < 10; i++)
        {
            Step(slowed);
            Step(twin);
        }
        Assert.True(slowed.IsSlowed);
        AssertClose(Speed(twin), Speed(slowed));

        var slowedStep = Step(slowed);
        var twinStep = Step(twin);
        Assert.True(twinStep.Length() > 1f);
        AssertClose(twinStep * 0.25f, slowedStep);
    }

    [Fact]
    public void Slowed_cursor_matches_its_unhit_twins_speed_and_movement_once_the_slow_ends()
    {
        var slowed = Target(BolaSpot, CursorMovement(), EnemyRoster.Cursor);
        var twin = Target(BolaSpot, CursorMovement(), EnemyRoster.Cursor);
        Hit(WeaponPath.Bola, slowed);

        // 2.5 s of the same frames: past the 2 s slow.
        for (var i = 0; i < 25; i++)
        {
            Step(slowed);
            Step(twin);
        }
        Assert.False(slowed.IsSlowed);

        Assert.True(Speed(twin) > EnemyRoster.Cursor.Speed);
        AssertClose(Speed(twin), Speed(slowed));
        AssertClose(Step(twin), Step(slowed));
    }

    // ---- Snaps and clamps: the slow scales the real move, not the move's length limit ----

    [Fact]
    public void Slowed_blink_on_a_jump_frame_moves_a_quarter_of_its_unhit_twins_jump_onto_the_player()
    {
        // Jump length J = 90 every 1.75 s; the player is d = 60 away, between J/4 and J, so an unslowed jump lands on them.
        var slowed = Target(BolaSpot, new Blink(10f, 90f, 1.75f));
        var twin = Target(BolaSpot, new Blink(10f, 90f, 1.75f));
        Hit(WeaponPath.Bola, slowed);
        Assert.True(slowed.IsSlowed);
        var player = BolaSpot + new Vector2(0, 60f);

        var slowedStep = StepToward(slowed, player, 1.75f);
        var twinStep = StepToward(twin, player, 1.75f);

        AssertClose(new Vector2(0, 60f), twinStep);
        AssertClose(twinStep * 0.25f, slowedStep);
        AssertClose(new Vector2(0, 15f), slowedStep);
    }

    public static TheoryData<string> NearPlayerPatterns => new() { "chase", "spiral", "weave", "thrown" };

    [Theory]
    [MemberData(nameof(NearPlayerPatterns))]
    public void Slowed_enemy_reaching_the_player_or_its_spot_this_frame_moves_a_quarter_as_far_as_its_twin(string pattern)
    {
        // The target is 3 pixels away, closer than one unslowed 0.1 s step, so the unslowed move stops on it.
        var target = BolaSpot + new Vector2(0, 3f);
        var slowed = Target(BolaSpot, NearPattern(pattern, target));
        var twin = Target(BolaSpot, NearPattern(pattern, target));
        Hit(WeaponPath.Bola, slowed);
        Assert.True(slowed.IsSlowed);

        var slowedStep = StepToward(slowed, target, Dt);
        var twinStep = StepToward(twin, target, Dt);

        Assert.True(twinStep.Length() > 1f);
        AssertClose(twinStep * 0.25f, slowedStep);
    }

    [Fact]
    public void Repeatedly_slowed_thrown_enemy_lands_within_bounded_frames()
    {
        var target = BolaSpot + new Vector2(0, 50f);
        var flightSpeed = 100f;
        var chaseSpeed = 60f;
        var thrown = new Thrown(target, flightSpeed, new ChasePlayer(chaseSpeed));
        var enemy = Target(BolaSpot, thrown);

        // Keep the enemy slowed continuously across frames.
        // Unslowed distance is 50, flightSpeed is 100, dt is 0.1s.
        // Under slow, each frame it moves toward target. With distance-based progression (remaining distance <= step),
        // it lands once remaining distance to target is within the slowed step.
        // But with `HasLanded = next == LandingSpot` where next = Displacement.Scale(position, LandingSpot, 0.25f),
        // next = position + (LandingSpot - position)*0.25f, so next only covers 25% of the remaining distance to LandingSpot,
        // never reaching LandingSpot (Zeno's paradox), so HasLanded is never true!
        const int maxFrames = 50;
        for (var frames = 0; frames < maxFrames && !thrown.HasLanded; frames++)
        {
            Hit(WeaponPath.Bola, enemy);
            StepToward(enemy, target, Dt);
        }

        Assert.True(thrown.HasLanded, $"Thrown enemy did not land within {maxFrames} frames (position={enemy.Position}, target={target})");
        AssertClose(target, enemy.Position);
    }

    // ---- Bosses and shields ----

    [Fact]
    public void Boss_slowed_by_the_hit_that_starts_a_new_phase_moves_at_a_quarter_of_the_new_phases_speed()
    {
        var bossType = Speck with
        {
            HitPoints = 4f,
            Phases =
            [
                new BossPhase(0.75f, CreateMovement: () => new ChasePlayer(120f),
                    Shield: new EnemyShield(Speck, 2, 150f, 1f)),
            ],
        };
        var boss = Target(BolaSpot, new ChasePlayer(30f), bossType);
        boss.TakeNewShields();

        Hit(WeaponPath.Bola, boss);

        Assert.Equal(1, boss.PhasesReached);
        Assert.IsType<ChasePlayer>(boss.Movement);
        Assert.True(boss.IsShielded);
        AssertClose(0.25f * 120f * Dt, Step(boss).Length());

        // The shield the phase spawned is a separate enemy the bola never touched: it orbits at full speed.
        Assert.Equal(2, boss.Shields.Count);
        foreach (var shield in boss.Shields)
        {
            Assert.False(shield.IsSlowed);
            var orbit = Assert.IsType<Orbit>(shield.Movement);
            var before = orbit.Angle;
            shield.Update(Player, Dt);
            AssertClose(Speck.Speed / 150f * Dt, orbit.Angle - before);
        }
    }

    [Fact]
    public void Shielded_boss_hit_by_a_bola_takes_no_damage_and_is_not_slowed()
    {
        var bossType = Speck with { HitPoints = 4f, Shield = new EnemyShield(Speck, 1, 200f, 1f) };
        var boss = Target(BolaSpot, new ChasePlayer(60f), bossType);

        var session = Hit(WeaponPath.Bola, boss);

        Assert.True(boss.IsShielded);
        Assert.Equal(4f, boss.HitPoints);
        // The bola did reach it: the hit was spent on the boss, just blocked by the shield.
        Assert.Empty(session.Lasers);
        Assert.False(boss.IsSlowed);
        AssertClose(60f * Dt, Step(boss).Length());
    }

    // ---- Only bolas slow ----

    [Theory]
    [InlineData(WeaponPath.Ballista)]
    [InlineData(WeaponPath.None)]
    public void Ballista_and_laser_hits_do_not_slow(WeaponPath path)
    {
        var enemy = Target(ShaftSpot, new ChasePlayer(60f));

        Hit(path, enemy);

        Assert.Equal(2f, enemy.HitPoints);
        Assert.False(enemy.IsSlowed);
        AssertClose(60f * Dt, Step(enemy).Length());
    }

    // ---- Ballista pierce ----

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 4)]
    public void Ballista_bolt_pierces_two_more_enemies_than_the_players_piercing(int piercingUpgrades, int damaged)
    {
        var session = Session(WeaponPath.Ballista);
        for (var i = 0; i < piercingUpgrades; i++)
            Upgrades.Apply(Upgrade.Piercing, session.Stats);
        Assert.Equal(piercingUpgrades, session.Stats.Pierce);

        // Six stationary specks in a column up the bolt's 52-pixel shaft.
        var line = Enumerable.Range(0, 6)
            .Select(i => TestEnemies.Stationary(Speck, Nose - new Vector2(0, 6f + 8f * i)))
            .ToArray();
        foreach (var enemy in line)
            session.AddEnemy(enemy);

        session.Update(Vector2.Zero, null, 1f, Width, Height);

        Assert.Equal(damaged, line.Count(enemy => enemy.HitPoints < Speck.HitPoints));
        Assert.Empty(session.Lasers);
    }

    // ---- Helpers ----

    /// <summary>A Gemini (3 hit points) whose outline is one tiny horizontal segment.</summary>
    private static EnemyType Speck => EnemyRoster.Gemini with { Outline = [[new(-0.5f, 0), new(0.5f, 0)]] };

    private static IMovementPattern Pattern(string name) => name switch
    {
        "chase" => new ChasePlayer(60f),
        "spiral" => new SpiralIn(80f, 40f),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static IMovementPattern NearPattern(string name, Vector2 target) => name switch
    {
        "chase" => new ChasePlayer(60f),
        "spiral" => new SpiralIn(60f, 40f),
        "weave" => new Weave(60f, 5f, 1f),
        "thrown" => new Thrown(target, 60f, new ChasePlayer(60f)),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static Vector2 StepToward(Enemy enemy, Vector2 player, float seconds)
    {
        var start = enemy.Position;
        enemy.Update(player, seconds);
        return enemy.Position - start;
    }

    private static Enemy Target(Vector2 position, IMovementPattern movement, EnemyType? type = null) =>
        new(type ?? Speck, position, new Held(movement));

    private static IMovementPattern CursorMovement() => EnemyRoster.Cursor.CreateMovement(EnemyRoster.Cursor.Speed);

    private static float Speed(Enemy enemy) => Assert.IsType<Accelerate>(((Held)enemy.Movement).Inner).CurrentSpeed;

    private static GameSession Session(WeaponPath path)
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.WeaponPath = path;
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 1_000;
        return session;
    }

    /// <summary>Fires one volley along `path` at the enemy through a fresh session's Update, in one 1 ms frame.</summary>
    private static GameSession Hit(WeaponPath path, Enemy enemy)
    {
        // Taken before the hit, since a phase change can replace the movement.
        var held = (Held)enemy.Movement;
        held.Holding = true;
        var session = Session(path);
        session.Stats.AttacksPerSecond = 1000f;
        session.AddEnemy(enemy);
        session.Update(Vector2.Zero, null, 0.001f, Width, Height);
        held.Holding = false;
        return session;
    }

    private static Vector2 Step(Enemy enemy)
    {
        var start = enemy.Position;
        enemy.Update(Player, Dt);
        return enemy.Position - start;
    }

    // Lets game time pass while the enemy holds its spot (so a later volley still finds it); its slow still runs down.
    private static void Advance(Enemy enemy, float seconds)
    {
        var held = (Held)enemy.Movement;
        held.Holding = true;
        var steps = (int)MathF.Round(seconds / 0.01f);
        for (var i = 0; i < steps; i++)
            enemy.Update(Player, 0.01f);
        held.Holding = false;
    }

    private static void AssertClose(float expected, float actual) =>
        Assert.True(MathF.Abs(expected - actual) < 1e-3f, $"Expected {expected}, got {actual}");

    private static void AssertClose(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 1e-3f, $"Expected {expected}, got {actual}");

    /// <summary>Holds still while a test's session fires at it, then moves with the wrapped real pattern.</summary>
    private sealed class Held(IMovementPattern inner) : IMovementPattern
    {
        public bool Holding { get; set; }

        public IMovementPattern Inner => inner;

        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) =>
            Move(position, playerPosition, elapsedSeconds, 1f);

        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor) =>
            Holding ? position : inner.Move(position, playerPosition, elapsedSeconds, speedFactor);
    }
}
