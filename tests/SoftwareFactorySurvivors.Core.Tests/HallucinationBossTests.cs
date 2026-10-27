using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class HallucinationBossTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);
    private const float Frame = 1f / 60f;

    [Fact]
    public void Split_into_copies_spawns_copies_with_same_type_radius_appearance_zero_xp_and_is_copy_true()
    {
        var boss = new Enemy(
            EnemyRoster.Hallucination,
            new Vector2(200, 200),
            movement: new StationaryPattern());

        var ability = new SplitIntoCopies(secondsBetweenSplits: 2f, copiesPerSplit: 2, spawnDistance: 50f);

        // Advance before interval
        var early = ability.Update(boss, Center, 1f, new Random(42));
        Assert.Empty(early);

        // Advance past interval
        var copies = ability.Update(boss, Center, 1.1f, new Random(42));
        Assert.Equal(2, copies.Count);

        foreach (var copy in copies)
        {
            Assert.True(copy.IsCopy);
            Assert.Null(copy.Ability);
            Assert.Same(boss.Type, copy.Type);
            Assert.Equal(boss.Type.Radius, copy.Type.Radius);
            Assert.Equal(0, copy.Experience);
            Assert.Equal(boss.Type.HitPoints, copy.HitPoints);
            Assert.False(copy.IsFlickering);
            Assert.Equal(0f, copy.FlickerSecondsLeft);
            Assert.Equal(boss.Type.Outline.Count, copy.GetStrokes().Count);
        }
    }

    [Fact]
    public void Copy_dies_in_one_hit_grants_zero_xp_and_does_not_lower_boss_hitpoints()
    {
        var session = TestEnemies.QuietSession(Center);
        var boss = new Enemy(
            EnemyRoster.Hallucination,
            new Vector2(200, 200),
            movement: new StationaryPattern());
        session.AddEnemy(boss);

        var copy = new Enemy(
            EnemyRoster.Hallucination,
            new Vector2(Center.X, Center.Y - 150f),
            movement: new StationaryPattern(),
            experience: 0,
            isCopy: true);
        session.AddEnemy(copy);

        Assert.Equal(80f, boss.HitPoints);
        Assert.Equal(80f, copy.HitPoints);
        Assert.Equal(0, session.Score);

        var initialLevel = session.Leveling.Level;
        var initialExperience = session.Leveling.Experience;

        // Fire laser straight up at the copy
        // In quiet session, ship fires 1 attack per second up from nose: the first shot leaves on the 60th frame
        // and still has ~100 px to fly to the copy, so allow 2 s for it to land.
        for (var i = 0; i < 120 && !copy.IsDead; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.True(copy.IsDead);
        Assert.Equal(0f, copy.HitPoints);
        // Leveling progress and experience must be unchanged (0 XP granted)
        Assert.Equal(initialLevel, session.Leveling.Level);
        Assert.Equal(initialExperience, session.Leveling.Experience);
        // Boss should NOT have taken damage
        Assert.Equal(80f, boss.HitPoints);
        Assert.False(copy.IsFlickering);
        // Ship should NOT have taken contact damage because copy was out of contact range
        Assert.Equal(session.Stats.MaxHitPoints, session.Stats.HitPoints);

        // Verify copy dying directly via TakeDamage as well
        var directCopy = new Enemy(EnemyRoster.Hallucination, Vector2.Zero, isCopy: true);
        directCopy.TakeDamage(1f);
        Assert.True(directCopy.IsDead);
        Assert.Equal(0f, directCopy.HitPoints);
        Assert.False(directCopy.IsFlickering);
        Assert.Equal(0f, directCopy.FlickerSecondsLeft);
    }

    [Fact]
    public void Real_boss_takes_normal_damage_and_flickers_flicker_clears_after_duration()
    {
        var boss = new Enemy(
            EnemyRoster.Hallucination,
            new Vector2(200, 200),
            movement: new StationaryPattern());

        Assert.False(boss.IsCopy);
        Assert.False(boss.IsFlickering);
        Assert.Equal(0f, boss.FlickerSecondsLeft);

        boss.TakeDamage(10f);

        Assert.Equal(70f, boss.HitPoints);
        Assert.True(boss.IsFlickering);
        Assert.True(boss.FlickerSecondsLeft > 0f);
        Assert.InRange(boss.FlickerSecondsLeft, 0.29f, 0.31f);

        // Update partially through flicker duration
        boss.Update(Center, 0.15f);
        Assert.True(boss.IsFlickering);
        Assert.InRange(boss.FlickerSecondsLeft, 0.14f, 0.16f);

        // Update past remaining duration
        boss.Update(Center, 0.2f);
        Assert.False(boss.IsFlickering);
        Assert.Equal(0f, boss.FlickerSecondsLeft);
    }

    [Fact]
    public void Copy_never_flickers_even_when_damaged()
    {
        var copy = new Enemy(
            EnemyRoster.Hallucination,
            new Vector2(100, 100),
            isCopy: true);

        Assert.False(copy.IsFlickering);
        copy.TakeDamage(5f);
        Assert.True(copy.IsDead);
        Assert.False(copy.IsFlickering);
        Assert.Equal(0f, copy.FlickerSecondsLeft);

        copy.Update(Center, 0.5f);
        Assert.False(copy.IsFlickering);
    }

    [Fact]
    public void Copy_touching_ship_costs_a_hit_like_non_boss_enemy_and_is_removed_on_survived_hit()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 3;

        // Place copy directly on the ship
        var copy = new Enemy(
            EnemyRoster.Hallucination,
            Center,
            movement: new StationaryPattern(),
            experience: 0,
            isCopy: true);
        session.AddEnemy(copy);

        Assert.Contains(copy, session.Enemies);

        // Step one frame to resolve contact
        session.Update(Vector2.Zero, null, Frame, Width, Height);

        // Ship took 1 hit
        Assert.Equal(2, session.Stats.HitPoints);
        // Copy is destroyed on contact because ship survived and copy is non-boss enemy
        Assert.DoesNotContain(copy, session.Enemies);
        Assert.True(session.InvulnerableSecondsLeft > 0f);
    }

    [Fact]
    public void Real_boss_is_the_only_hallucination_family_enemy_whose_hitpoints_can_reduce_short_of_killing()
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        var copy = new Enemy(EnemyRoster.Hallucination, Vector2.Zero, isCopy: true);

        boss.TakeDamage(10f);
        Assert.Equal(70f, boss.HitPoints); // reduced short of killing (80 -> 70)
        Assert.False(boss.IsDead);

        copy.TakeDamage(10f);
        Assert.Equal(0f, copy.HitPoints); // killed outright
        Assert.True(copy.IsDead);
    }

    [Fact]
    public void Copies_have_no_ability_and_do_not_spawn_copies_over_multiple_split_periods()
    {
        // Copies chase the ship at 70 px/s, so the ship sits far enough away (as in FarSession) that no copy
        // reaches it or crosses its line of fire within 16 s; every copy made stays alive to be counted.
        var session = TestEnemies.QuietSession(new Vector2(6000, 6000));
        var boss = new Enemy(
            EnemyRoster.Hallucination,
            new Vector2(300, 300),
            movement: new StationaryPattern());
        session.AddEnemy(boss);

        Assert.Null(new Enemy(EnemyRoster.Hallucination, Vector2.Zero, isCopy: true).Ability);
        Assert.NotNull(boss.Ability);

        // Advance across 3 split periods (Hallucination splits every 5s with 2 copies each).
        // 3 split periods = 15s. At each split, the boss spawns 2 copies.
        // If copies could split, the count would grow exponentially (2 -> 2 + 2*2 + ...).
        // Because copies have no ability, after 16 seconds there should be exactly
        // 1 boss + 3 * 2 = 6 copies (total 7 enemies, exactly 6 copies).
        const float dt = 0.5f;
        for (var t = 0f; t < 16f; t += dt)
        {
            session.Update(Vector2.Zero, null, dt, Width, Height);
        }

        var copies = session.Enemies.Where(e => e.IsCopy).ToList();
        Assert.Equal(6, copies.Count);
        Assert.All(copies, c => Assert.Null(c.Ability));

        // Direct ability check on a copy also returns empty
        var ability = new SplitIntoCopies(secondsBetweenSplits: 1f, copiesPerSplit: 2);
        var copy = new Enemy(EnemyRoster.Hallucination, Vector2.Zero, isCopy: true);
        var spawnedFromCopy = ability.Update(copy, Center, 2f, new Random(42));
        Assert.Empty(spawnedFromCopy);
    }

    // ---- Phases at 75%, 50% and 25% health ----

    // A step that is exact in binary floating point, so split times land on whole steps.
    private const float Step = 1f / 64f;
    private static readonly Vector2 FarShip = new(100_000f, 0f);

    // Hallucination has 80 HP: 75% = 60, 50% = 40, 25% = 20.
    private static Enemy StagedHallucination(int phases)
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        for (var i = 0; i < phases; i++)
            boss.TakeDamage(20f);
        Assert.Equal(phases, boss.PhasesReached);
        return boss;
    }

    // Runs the boss's ability step by step and returns (time of split, copies made) for each split, with time
    // measured from the start of this call.
    private static List<(float Time, int Copies)> Splits(Enemy boss, float seconds, Random? random = null)
    {
        random ??= new Random(7);
        var splits = new List<(float, int)>();
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 1; i <= steps; i++)
        {
            var copies = boss.UseAbility(FarShip, Step, random);
            if (copies.Count > 0)
                splits.Add((i * Step, copies.Count));
        }
        return splits;
    }

    private static void Advance(Enemy boss, float seconds)
    {
        var splits = Splits(boss, seconds);
        Assert.Empty(splits);
    }

    [Fact]
    public void Hallucination_phases_are_at_75_50_and_25_percent()
    {
        var phases = EnemyRoster.Hallucination.Phases;
        Assert.NotNull(phases);
        Assert.Equal([0.75f, 0.50f, 0.25f], phases.Select(p => p.AtHealthFraction));
        // No phase replaces the ability: the one split ability keeps its timer across every phase.
        Assert.All(phases, p => Assert.Null(p.CreateAbility));
    }

    [Theory]
    [InlineData(0, 5.0f, 2)]
    [InlineData(1, 3.5f, 2)]
    [InlineData(2, 3.5f, 3)]
    [InlineData(3, 2.5f, 3)]
    public void Hallucination_split_interval_and_copies_follow_phases_reached(int phases, float interval, int copies)
    {
        var boss = StagedHallucination(phases);

        var splits = Splits(boss, 3 * interval + Step / 2);

        Assert.Equal(3, splits.Count);
        for (var i = 0; i < splits.Count; i++)
        {
            Assert.Equal((i + 1) * interval, splits[i].Time);
            Assert.Equal(copies, splits[i].Copies);
        }
    }

    [Fact]
    public void Hallucination_moves_at_70_then_100_from_half_health_then_dashes_from_a_quarter()
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        var dt = 1f / 60f;

        float StepDistance()
        {
            var before = boss.Position;
            boss.Update(FarShip, dt);
            return Vector2.Distance(before, boss.Position);
        }

        Assert.Equal(70f * dt, StepDistance(), 3);

        var original = boss.Movement;
        boss.TakeDamage(20f); // 75%
        Assert.Equal(1, boss.PhasesReached);
        Assert.Same(original, boss.Movement);
        Assert.Equal(70f * dt, StepDistance(), 3);

        boss.TakeDamage(20f); // 50%
        Assert.Equal(2, boss.PhasesReached);
        Assert.Equal(100f * dt, StepDistance(), 3);

        boss.TakeDamage(20f); // 25%
        Assert.Equal(3, boss.PhasesReached);
        Assert.IsType<Dash>(boss.Movement);

        // It creeps, then lunges straight at where the ship is.
        var lunge = Enumerable.Range(0, 120).Select(_ => (Before: boss.Position, Distance: StepDistance()))
            .First(s => s.Distance > 300f * dt);
        Assert.Equal(420f * dt, lunge.Distance, 3);
    }

    [Fact]
    public void One_hit_from_full_health_to_20_percent_reaches_all_three_phases()
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        var ability = boss.Ability;

        boss.TakeDamage(64f); // 80 -> 16 HP = 20%

        Assert.False(boss.IsDead);
        Assert.Equal(3, boss.PhasesReached);
        Assert.Same(ability, boss.Ability);
        Assert.IsType<Dash>(boss.Movement);
        var splits = Splits(boss, 3 * 2.5f + Step / 2);
        Assert.Equal([2.5f, 5.0f, 7.5f], splits.Select(s => s.Time));
        Assert.All(splits, s => Assert.Equal(3, s.Copies));
    }

    [Fact]
    public void A_killing_blow_enters_no_phase()
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        var movement = boss.Movement;

        boss.TakeDamage(500f);

        Assert.True(boss.IsDead);
        Assert.Equal(0, boss.PhasesReached);
        Assert.Same(movement, boss.Movement);
    }

    [Fact]
    public void Split_wait_carries_over_75_percent_so_3s_in_the_next_split_lands_at_3_5s()
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        Advance(boss, 3.0f);

        boss.TakeDamage(20f); // 75%: interval 5 -> 3.5

        var splits = Splits(boss, 1.0f);
        Assert.Single(splits);
        Assert.Equal(3.5f, 3.0f + splits[0].Time);
    }

    [Fact]
    public void Overdue_split_fires_once_on_the_next_update_after_crossing_75_percent()
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        Advance(boss, 4.0f);

        boss.TakeDamage(20f);

        var first = boss.UseAbility(FarShip, Step, new Random(7));
        Assert.Equal(2, first.Count);
        // Exactly one: the remainder (0.5 s + one step) counts toward the next 3.5 s, so the next split comes
        // 3 s - one step after the overdue one, and none before it.
        var next = Splits(boss, 3.2f);
        Assert.Single(next);
        Assert.Equal(3.0f - Step, next[0].Time);
    }

    [Fact]
    public void Overdue_split_after_one_hit_to_a_quarter_fires_once_then_every_2_5s_less_the_remainder()
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        Advance(boss, 4.0f);

        boss.TakeDamage(64f); // one hit: 75%, 50% and 25% at once
        Assert.Equal(3, boss.PhasesReached);

        var first = boss.UseAbility(FarShip, Step, new Random(7));
        Assert.Equal(3, first.Count);
        // Banked after the split: 4 s + step - 2.5 s = 1.5 s + step, so the next is due 1 s - step later.
        var splits = Splits(boss, 2.0f);
        Assert.Equal(1.0f - Step, splits[0].Time, 3);
        Assert.Equal(3, splits[0].Copies);
    }

    [Fact]
    public void First_split_after_crossing_a_threshold_is_never_later_than_the_old_interval_would_have_made_it()
    {
        for (var offset = 0f; offset < 5f; offset += 0.25f)
        {
            foreach (var hit in new[] { 20f, 64f })
            {
                var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
                Advance(boss, offset);
                boss.TakeDamage(hit);
                var newInterval = hit == 20f ? 3.5f : 2.5f;

                var splits = Splits(boss, 5f);

                Assert.NotEmpty(splits);
                var firstAt = offset + splits[0].Time;
                Assert.True(firstAt <= 5.0f, $"offset {offset}, hit {hit}: first split at {firstAt}s, old due 5s");
                // And it is exactly when the kept wait reaches the new interval (or next update if already overdue).
                Assert.Equal(MathF.Max(newInterval, offset + Step), firstAt, 3);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void At_a_quarter_every_lunge_follows_at_least_one_second_of_creep(bool oneHit)
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);
        if (oneHit)
        {
            boss.TakeDamage(64f);
        }
        else
        {
            boss.TakeDamage(20f);
            boss.Update(FarShip, Frame);
            boss.TakeDamage(20f);
            boss.Update(FarShip, Frame);
            boss.TakeDamage(20f);
        }
        Assert.Equal(3, boss.PhasesReached);
        var dash = Assert.IsType<Dash>(boss.Movement);
        Assert.True(dash.CreepSeconds >= 1.0f);

        // Frame by frame for 10 s: count creep frames before each lunge.
        var creepRuns = new List<int>();
        var creep = 0;
        var dashing = false;
        var lunges = 0;
        for (var i = 0; i < 600; i++)
        {
            var before = boss.Position;
            boss.Update(FarShip, Frame);
            var lunging = Vector2.Distance(before, boss.Position) > 300f * Frame;
            if (lunging && !dashing)
            {
                creepRuns.Add(creep);
                lunges++;
            }
            if (!lunging)
                creep++;
            else
                creep = 0;
            dashing = lunging;
        }

        Assert.True(lunges >= 3, $"only {lunges} lunges in 10 s");
        // The first entry is the creep since the threshold; the rest are the gaps between lunges.
        Assert.All(creepRuns, frames => Assert.True(frames * Frame >= 1.0f, $"lunge after only {frames * Frame}s of creep"));
    }

    [Fact]
    public void Hallucination_through_all_phases_in_a_session_never_lays_a_trail()
    {
        var (session, boss) = FarSession(EnemyRoster.Hallucination);
        boss.TakeDamage(64f);
        Assert.Equal(3, boss.PhasesReached);

        var lunges = 0;
        for (var i = 0; i < 660; i++) // 11 s
        {
            var before = boss.Position;
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            if (Vector2.Distance(before, boss.Position) > 300f * Frame)
                lunges++;
            Assert.DoesNotContain(session.Hazards, h => h.Kind == HazardKind.Trail);
            Assert.All(session.Enemies, e => Assert.IsNotType<ChargeAlongLine>(e.Movement));
        }

        Assert.False(boss.IsDead);
        Assert.True(lunges > 0);
        Assert.True(session.Enemies.Count(e => e.IsCopy) >= 6, "several splits should have happened");
    }

    [Fact]
    public void Autocomplete_in_the_same_session_harness_does_lay_a_trail()
    {
        var (session, _) = FarSession(EnemyRoster.Autocomplete);

        var sawTrail = false;
        for (var i = 0; i < 660 && !sawTrail; i++)
        {
            session.Update(Vector2.Zero, null, Frame, Width, Height);
            sawTrail = session.Hazards.Any(h => h.Kind == HazardKind.Trail);
        }

        Assert.True(sawTrail);
    }

    // The ship sits far from the boss, so neither reaches the other and the ship's shots never hit it.
    private static (GameSession Session, Enemy Boss) FarSession(EnemyType type)
    {
        var session = TestEnemies.QuietSession(new Vector2(6000, 6000));
        session.Stats.MaxHitPoints = session.Stats.HitPoints = 50;
        var boss = new Enemy(type, new Vector2(200, 200));
        session.AddEnemy(boss);
        return (session, boss);
    }

    [Fact]
    public void Copies_still_die_in_one_hit_and_never_enter_a_phase()
    {
        var copy = new Enemy(EnemyRoster.Hallucination, Vector2.Zero, isCopy: true);
        var movement = copy.Movement;

        copy.TakeDamage(1f);

        Assert.True(copy.IsDead);
        Assert.Equal(0, copy.PhasesReached);
        Assert.Same(movement, copy.Movement);
        Assert.Null(copy.Ability);
    }

    // ---- Copies move like the boss, are capped at 8 and never spawn on top of you ----

    // Runs the split until it makes copies and returns them (the ship far away so no relocation happens).
    private static IReadOnlyList<Enemy> NextSplit(Enemy boss)
    {
        var random = new Random(3);
        for (var i = 0; i < 64 * 10; i++)
        {
            var copies = boss.UseAbility(FarShip, Step, random);
            if (copies.Count > 0)
                return copies;
        }
        throw new InvalidOperationException("no split in 10 s");
    }

    [Fact]
    public void Copies_split_after_a_phase_move_with_that_phases_pattern_each_with_its_own_instance()
    {
        var boss = new Enemy(EnemyRoster.Hallucination, Vector2.Zero);

        // Before any phase: the type's base movement, ChasePlayer at 70.
        var before = NextSplit(boss);
        Assert.All(before, c => Assert.Equal(70f, Assert.IsType<ChasePlayer>(c.Movement).Speed));

        // 75% sets no movement: still the base.
        boss.TakeDamage(20f);
        Assert.All(NextSplit(boss), c => Assert.Equal(70f, Assert.IsType<ChasePlayer>(c.Movement).Speed));

        // 50%: ChasePlayer at 100, like the boss.
        boss.TakeDamage(20f);
        var half = NextSplit(boss);
        Assert.Equal(3, half.Count);
        Assert.All(half, c => Assert.Equal(100f, Assert.IsType<ChasePlayer>(c.Movement).Speed));

        // 25%: Dash, like the boss.
        boss.TakeDamage(20f);
        var quarter = NextSplit(boss);
        Assert.Equal(3, quarter.Count);
        Assert.All(quarter, c => Assert.IsType<Dash>(c.Movement));
        Assert.IsType<Dash>(boss.Movement);

        // Every copy has its own instance, never the boss's or another copy's.
        var all = before.Concat(half).Concat(quarter).ToList();
        Assert.All(all, c => Assert.NotSame(boss.Movement, c.Movement));
        Assert.Equal(all.Count, all.Select(c => c.Movement).Distinct(ReferenceEqualityComparer.Instance).Count());

        // Advancing one dashing copy into its lunge leaves the other copy and the boss still creeping.
        var (a, b) = (quarter[0], quarter[1]);
        var ship = new Vector2(5000f, 0f);
        for (var i = 0; i < 80; i++) // 1.33 s: past a's 1.2 s creep
            a.Update(ship, Frame);
        float StepOf(Enemy e)
        {
            var p = e.Position;
            e.Update(ship, Frame);
            return Vector2.Distance(p, e.Position);
        }
        Assert.Equal(420f * Frame, StepOf(a), 3);
        Assert.Equal(100f * Frame, StepOf(b), 3);
        Assert.Equal(100f * Frame, StepOf(boss), 3);
    }

    // A session with a Hallucination boss at 50% (3 copies every 3.5 s) and the given number of live copies,
    // all far from the ship and out of its line of fire.
    private static (GameSession Session, Enemy Boss) CappedSession(int liveCopies)
    {
        var (session, boss) = FarSession(EnemyRoster.Hallucination);
        boss.TakeDamage(40f);
        Assert.Equal(2, boss.PhasesReached);
        for (var i = 0; i < liveCopies; i++)
            session.AddEnemy(new Enemy(EnemyRoster.Hallucination, new Vector2(100f + 20f * i, 100f),
                movement: new StationaryPattern(), experience: 0, isCopy: true));
        return (session, boss);
    }

    private static int CopiesIn(GameSession session) =>
        session.Enemies.Count(e => e.IsCopy && e.Type == EnemyRoster.Hallucination);

    // Steps the session, checking the cap every frame.
    private static void RunCapped(GameSession session, float seconds)
    {
        var steps = (int)MathF.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
        {
            session.Update(Vector2.Zero, null, Step, Width, Height);
            Assert.True(CopiesIn(session) <= 8, $"{CopiesIn(session)} live copies");
        }
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(6, 2)]
    [InlineData(7, 1)]
    [InlineData(8, 0)]
    public void A_3_copy_split_adds_only_what_fits_under_8_live_copies(int alive, int added)
    {
        var (session, _) = CappedSession(alive);

        RunCapped(session, 3.5f);

        Assert.Equal(alive + added, CopiesIn(session));
    }

    [Fact]
    public void At_8_copies_splits_make_none_but_still_use_up_their_interval_and_resume_after_a_copy_is_gone()
    {
        var (session, _) = CappedSession(8);

        // Three splits come due (3.5, 7, 10.5 s) and make nothing.
        RunCapped(session, 10.5f + 2 * Step);
        Assert.Equal(8, CopiesIn(session));

        // Shoot one copy just after the third split was due at the cap.
        var victim = session.Enemies.First(e => e.IsCopy);
        victim.TakeDamage(1f);
        RunCapped(session, Step);
        Assert.Equal(7, CopiesIn(session));

        // That split used up its interval: nothing comes until the next one is due at 14 s.
        RunCapped(session, 14f - 10.5f - 4 * Step);
        Assert.Equal(7, CopiesIn(session));

        // The next due split refills to the cap: exactly 1 copy.
        RunCapped(session, 2 * Step);
        Assert.Equal(8, CopiesIn(session));
    }

    [Fact]
    public void Live_copies_never_exceed_8_across_every_phase()
    {
        var (session, boss) = FarSession(EnemyRoster.Hallucination);
        var max = 0;
        foreach (var phaseHit in new[] { 0f, 20f, 20f, 20f })
        {
            boss.TakeDamage(phaseHit);
            var steps = (int)MathF.Round(8f / Step);
            for (var i = 0; i < steps; i++)
            {
                session.Update(Vector2.Zero, null, Step, Width, Height);
                max = Math.Max(max, CopiesIn(session));
                Assert.True(CopiesIn(session) <= 8, $"{CopiesIn(session)} live copies at phase {boss.PhasesReached}");
            }
        }
        Assert.Equal(3, boss.PhasesReached);
        Assert.Equal(8, max);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(30f)]
    [InlineData(80f)]
    [InlineData(150f)]
    public void Copies_spawn_at_least_100px_from_the_ship_relocated_to_the_far_side_of_the_boss(float shipDistance)
    {
        var bossAt = new Vector2(400f, 300f);
        var ship = bossAt + new Vector2(0.6f, -0.8f) * shipDistance;
        var shipToBoss = shipDistance > 0f ? Vector2.Normalize(bossAt - ship) : Vector2.Zero;
        var relocated = 0;

        for (var seed = 0; seed < 200; seed++)
        {
            var boss = new Enemy(EnemyRoster.Hallucination, bossAt, movement: new StationaryPattern());
            var near = new SplitIntoCopies(secondsBetweenSplits: 1f, copiesPerSplit: 3);
            var far = new SplitIntoCopies(secondsBetweenSplits: 1f, copiesPerSplit: 3);

            // Same timing and count as a split with the ship far away (no relocation).
            for (var i = 1; i <= 64 * 3; i++)
            {
                var nearCopies = near.Update(boss, ship, Step, new Random(seed));
                var farCopies = far.Update(boss, FarShip, Step, new Random(seed));
                Assert.Equal(farCopies.Count, nearCopies.Count);
                if (nearCopies.Count == 0)
                    continue;

                // Replay the random angles to know where each copy would have gone unrelocated.
                var replay = new Random(seed);
                foreach (var copy in nearCopies)
                {
                    var angle = replay.NextSingle() * MathF.Tau;
                    var spot = bossAt + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 60f;
                    Assert.True(Vector2.Distance(copy.Position, ship) >= 100f,
                        $"seed {seed}: copy {Vector2.Distance(copy.Position, ship)} px from the ship");
                    if (Vector2.Distance(spot, ship) >= 100f)
                    {
                        Assert.Equal(spot, copy.Position);
                        continue;
                    }
                    relocated++;
                    Assert.True(Vector2.Dot(copy.Position - bossAt, shipToBoss) >= -1e-3f,
                        $"seed {seed}: relocated copy is on the ship's side of the boss");
                }
            }
        }

        // From 30 px in, some random spot is always too close; at 150 px only some angles are (60 px reaches 90 px).
        if (shipDistance < 100f)
            Assert.True(relocated > 0);
    }

    [Fact]
    public void Copies_of_any_phase_die_in_one_hit_never_enter_a_phase_and_outlive_the_bosss_next_threshold()
    {
        var (session, boss) = FarSession(EnemyRoster.Hallucination);

        // A copy split before any phase change.
        RunCapped(session, 5f);
        var early = session.Enemies.First(e => e.IsCopy);

        // The boss crosses 75% and 50%: the early copy is still in play.
        boss.TakeDamage(20f);
        RunCapped(session, Step);
        boss.TakeDamage(20f);
        RunCapped(session, Step);
        Assert.Equal(2, boss.PhasesReached);
        Assert.Contains(early, session.Enemies);

        // A copy split under the 50% recipe dies in one hit and keeps PhasesReached 0.
        var phaseCopy = NextSplit(boss)[0];
        Assert.IsType<ChasePlayer>(phaseCopy.Movement);
        phaseCopy.TakeDamage(1f);
        Assert.True(phaseCopy.IsDead);
        Assert.Equal(0, phaseCopy.PhasesReached);
        Assert.Equal(0, phaseCopy.Experience);
        Assert.Equal(40f, boss.HitPoints);
    }

    private sealed class StationaryPattern : IMovementPattern
    {
        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) =>
            position;

        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor) =>
            position;
    }
}
