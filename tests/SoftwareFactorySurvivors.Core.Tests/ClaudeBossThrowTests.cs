using System.Numerics;
using System.Reflection;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ClaudeBossThrowTests
{
    private static readonly Vector2 BossCenter = new(500, 400);
    private static readonly Vector2 PlayerPosition = new(500, 200);

    [Fact]
    public void ClaudeBoss_throws_no_claudes_at_full_health_or_30_percent_health_for_20s()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, BossCenter);
        var rng = new Random(42);

        // (a) At full health, returns no EnemyRoster.Claude for 20 s
        for (var i = 0; i < 200; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }

        // Damage boss down to 30% health (120 * 0.30 = 36 hit points; takes 84 damage)
        boss.TakeDamage(120f - 36f);
        Assert.Equal(36f, boss.HitPoints);
        Assert.Equal(2, boss.PhasesReached);

        // At 30% health, returns no EnemyRoster.Claude for 20 s
        for (var i = 0; i < 200; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }
    }

    [Fact]
    public void ClaudeBoss_staged_damage_to_25_percent_fires_at_2s_6s_and_10s()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, BossCenter);
        var rng = new Random(101);

        // First damage to 50% (crosses 75% threshold)
        boss.TakeDamage(60f);
        Assert.Equal(2, boss.PhasesReached);

        // Take damage to 25% (crosses 25% threshold)
        boss.TakeDamage(30f);
        Assert.Equal(30f, boss.HitPoints);
        Assert.Equal(3, boss.PhasesReached);

        // (b) No Claude arrives before 2.0 s
        for (var i = 0; i < 19; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }

        // Exactly 2 arrive at 2.0 s (step 20)
        var wave1 = boss.UseAbility(PlayerPosition, 0.1f, rng);
        Assert.Equal(2, wave1.Count);
        Assert.All(wave1, e => Assert.Same(EnemyRoster.Claude, e.Type));

        // No Claude between 2.1 s and 5.9 s (39 steps)
        for (var i = 0; i < 39; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }

        // Exactly 2 arrive at 6.0 s (step 60 from start)
        var wave2 = boss.UseAbility(PlayerPosition, 0.1f, rng);
        Assert.Equal(2, wave2.Count);
        Assert.All(wave2, e => Assert.Same(EnemyRoster.Claude, e.Type));

        // No Claude between 6.1 s and 9.9 s (39 steps)
        for (var i = 0; i < 39; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }

        // Exactly 2 arrive at 10.0 s (step 100 from start)
        var wave3 = boss.UseAbility(PlayerPosition, 0.1f, rng);
        Assert.Equal(2, wave3.Count);
        Assert.All(wave3, e => Assert.Same(EnemyRoster.Claude, e.Type));
    }

    [Fact]
    public void ClaudeBoss_single_hit_from_full_health_to_20_percent_fires_at_2s_6s_and_10s()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, BossCenter);
        var rng = new Random(202);

        // (c) One TakeDamage from full health to 20% (120 -> 24 hit points; takes 96 damage)
        boss.TakeDamage(96f);
        Assert.Equal(24f, boss.HitPoints);
        Assert.Equal(3, boss.PhasesReached);

        // No Claude arrives before 2.0 s
        for (var i = 0; i < 19; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }

        // Exactly 2 arrive at 2.0 s
        var wave1 = boss.UseAbility(PlayerPosition, 0.1f, rng);
        Assert.Equal(2, wave1.Count);
        Assert.All(wave1, e => Assert.Same(EnemyRoster.Claude, e.Type));

        // No Claude between 2.1 s and 5.9 s
        for (var i = 0; i < 39; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }

        // Exactly 2 arrive at 6.0 s
        var wave2 = boss.UseAbility(PlayerPosition, 0.1f, rng);
        Assert.Equal(2, wave2.Count);
        Assert.All(wave2, e => Assert.Same(EnemyRoster.Claude, e.Type));

        // No Claude between 6.1 s and 9.9 s
        for (var i = 0; i < 39; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }

        // Exactly 2 arrive at 10.0 s
        var wave3 = boss.UseAbility(PlayerPosition, 0.1f, rng);
        Assert.Equal(2, wave3.Count);
        Assert.All(wave3, e => Assert.Same(EnemyRoster.Claude, e.Type));
    }

    [Fact]
    public void ClaudeBoss_killing_blow_enters_no_phase_and_causes_no_throw()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, BossCenter);
        var rng = new Random(303);

        boss.TakeDamage(boss.Type.HitPoints);
        Assert.True(boss.IsDead);
        Assert.Equal(0, boss.PhasesReached);

        for (var i = 0; i < 100; i++)
        {
            var enemies = boss.UseAbility(PlayerPosition, 0.1f, rng);
            Assert.Empty(enemies);
        }
    }

    [Fact]
    public void ClaudeBoss_crossing_25_percent_preserves_ability_instances_and_fires_no_extra_ring()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, BossCenter);
        var rng = new Random(404);

        var composite = Assert.IsType<CompositeAbility>(boss.Ability);
        var closingRingBefore = composite.Abilities.OfType<ClosingRing>().Single();
        var bulletBurstsBefore = composite.Abilities.OfType<BulletBursts>().Single();

        // Initial update: ClosingRing immediately fires its first ring hazard
        boss.UseAbility(PlayerPosition, 0.1f, rng);
        var hazards = composite.TakeHazards();
        Assert.Contains(hazards, h => h.Kind == HazardKind.Ring);

        // Advance 2.0 s (total time 2.1 s, well below ClosingRing's 10 s cooldown)
        boss.UseAbility(PlayerPosition, 2.0f, rng);
        hazards = composite.TakeHazards();
        Assert.DoesNotContain(hazards, h => h.Kind == HazardKind.Ring);

        // Cross 25% threshold (120 * 0.80 = 96 damage -> 24 HP remaining)
        boss.TakeDamage(96f);
        Assert.Equal(3, boss.PhasesReached);

        // The composite and internal ability instances are preserved
        var compositeAfter = Assert.IsType<CompositeAbility>(boss.Ability);
        Assert.Same(composite, compositeAfter);
        var closingRingAfter = compositeAfter.Abilities.OfType<ClosingRing>().Single();
        var bulletBurstsAfter = compositeAfter.Abilities.OfType<BulletBursts>().Single();
        Assert.Same(closingRingBefore, closingRingAfter);
        Assert.Same(bulletBurstsBefore, bulletBurstsAfter);

        // On the threshold update, no HazardKind.Ring is produced
        boss.UseAbility(PlayerPosition, 0.1f, rng);
        hazards = composite.TakeHazards();
        Assert.DoesNotContain(hazards, h => h.Kind == HazardKind.Ring);
    }

    [Fact]
    public void ClaudeBoss_throw_creates_valid_claudes_with_thrown_movement_in_range_and_dash_movement()
    {
        var totalMinionsTested = 0;

        // Across at least 200 minions over several Random seeds
        for (var seed = 1; seed <= 10; seed++)
        {
            var rng = new Random(seed);
            var boss = new Enemy(EnemyRoster.ClaudeBoss, BossCenter);
            boss.TakeDamage(boss.Type.HitPoints * 0.75f); // <= 25% health

            // Advance past 2.0s initial delay
            boss.UseAbility(PlayerPosition, 2.0f, rng);

            // Collect 10 throws (20 minions per seed * 10 seeds = 200 minions)
            for (var throwIdx = 0; throwIdx < 10; throwIdx++)
            {
                var minions = boss.UseAbility(PlayerPosition, 4.0f, rng);
                Assert.Equal(2, minions.Count);

                var minion1 = minions[0];
                var minion2 = minions[1];

                var thrown1 = Assert.IsType<Thrown>(minion1.Movement);
                var thrown2 = Assert.IsType<Thrown>(minion2.Movement);

                // Each minion gets its own landing spot
                Assert.NotEqual(thrown1.LandingSpot, thrown2.LandingSpot);

                foreach (var minion in minions)
                {
                    totalMinionsTested++;
                    Assert.Same(EnemyRoster.Claude, minion.Type);
                    Assert.Equal(0, minion.Experience);
                    Assert.Equal(boss.Position, minion.Position);

                    var thrown = Assert.IsType<Thrown>(minion.Movement);
                    var distanceToPlayer = Vector2.Distance(thrown.LandingSpot, PlayerPosition);
                    Assert.True(distanceToPlayer >= 150f - 0.001f && distanceToPlayer <= 250f + 0.001f,
                        $"LandingSpot distance {distanceToPlayer} outside [150, 250]");

                    // Flight speed is 4x Claude speed (60f * 4 = 240f)
                    var testFlightPos = thrown.Move(Vector2.Zero, PlayerPosition, 0.1f);
                    Assert.Equal(240f * 0.1f, testFlightPos.Length(), precision: 2);

                    // After landing it moves with a freshly built Claude Dash
                    var afterLandingField = typeof(Thrown)
                        .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(f => typeof(IMovementPattern).IsAssignableFrom(f.FieldType));
                    var afterLanding = afterLandingField?.GetValue(thrown);
                    Assert.IsType<Dash>(afterLanding);
                }
            }
        }

        Assert.True(totalMinionsTested >= 200, $"Expected >= 200 minions tested, got {totalMinionsTested}");
    }

    [Fact]
    public void ClaudeBoss_throw_gate_looks_up_by_health_fraction_not_hardcoded_index()
    {
        // When sibling tasks add a 0.50 entry above 0.25 (so 0.25 is at index 2),
        // the gate activates at the 0.25 entry, not index 1.
        var multiPhaseType = new EnemyType(
            "Test Claude Boss", Radius: 70f, HitPoints: 120f, Speed: 80f, Experience: 250,
            DefaultSpawnsPerMinute: 0f, ArrivesAtSeconds: float.PositiveInfinity, SpinSpeed: 0f,
            EnemyRoster.ClaudeBoss.CreateMovement, LogoShapes.Claude(70f),
            EnemyRoster.ClaudeBoss.CreateAbility,
            Phases:
            [
                new(0.75f),
                new(0.50f),
                new(0.25f),
            ]);

        var boss = new Enemy(multiPhaseType, BossCenter);
        var rng = new Random(505);

        // Crossing 0.75 (PhasesReached = 1) -> no throw
        boss.TakeDamage(120f * 0.25f);
        Assert.Equal(1, boss.PhasesReached);
        for (var i = 0; i < 50; i++)
            Assert.Empty(boss.UseAbility(PlayerPosition, 0.1f, rng));

        // Crossing 0.50 (PhasesReached = 2) -> no throw (index 1 is 0.50, not 0.25!)
        boss.TakeDamage(120f * 0.25f);
        Assert.Equal(2, boss.PhasesReached);
        for (var i = 0; i < 50; i++)
            Assert.Empty(boss.UseAbility(PlayerPosition, 0.1f, rng));

        // Crossing 0.25 (PhasesReached = 3) -> now gate opens!
        boss.TakeDamage(120f * 0.25f);
        Assert.Equal(3, boss.PhasesReached);

        // After 2.0 s, throws Claudes
        for (var i = 0; i < 19; i++)
            Assert.Empty(boss.UseAbility(PlayerPosition, 0.1f, rng));

        var minions = boss.UseAbility(PlayerPosition, 0.1f, rng);
        Assert.Equal(2, minions.Count);
        Assert.All(minions, m => Assert.Same(EnemyRoster.Claude, m.Type));
    }

    [Fact]
    public void ClaudeBoss_thrown_claude_is_harmless_in_flight_and_damages_after_landing()
    {
        // CLM-1: In a GameSession, an enemy thrown by ClaudeBoss whose Thrown movement has not landed
        // never costs the ship a hit point. It is never removed by touching the ship and never starts
        // the ship's invulnerability. Once its Thrown.HasLanded is true, touching the ship costs a hit point
        // and removes it, exactly like any other regular enemy.
        // Discriminating case: an airborne minion thrown by GeminiBoss still hurts on contact, as today.
        var shipPos = new Vector2(500, 400);

        // Case 1: Airborne Claude Boss-thrown Claude placed on the ship's strokes
        var session1 = TestEnemies.QuietSession(shipPos);
        var boss = new Enemy(EnemyRoster.ClaudeBoss, shipPos);
        boss.TakeDamage(boss.Type.HitPoints * 0.75f); // reach <= 25% health phase
        var rng = new Random(123);
        // Advance 2.0s to get thrown minions
        var minions = boss.UseAbility(shipPos, 2.0f, rng);
        Assert.Equal(2, minions.Count);
        var claudeMinion = minions[0];
        Assert.Same(EnemyRoster.Claude, claudeMinion.Type);

        var thrownMovement = Assert.IsType<Thrown>(claudeMinion.Movement);
        Assert.True(thrownMovement.IsHarmlessInFlight);
        Assert.False(thrownMovement.HasLanded);

        // Position the airborne Claude directly on the ship
        // We simulate this by adding the minion to session1 at the ship's position
        // Claude minion position is currently boss.Position which is shipPos!
        session1.AddEnemy(claudeMinion);
        Assert.Equal(shipPos, claudeMinion.Position);
        Assert.Equal(1, session1.Stats.HitPoints);
        Assert.Equal(0f, session1.InvulnerableSecondsLeft);

        // Update session
        session1.Update(Vector2.Zero, null, 0.01f, 1000f, 800f);

        // HitPoints unchanged, minion still in Enemies, InvulnerableSecondsLeft is 0
        Assert.Equal(1, session1.Stats.HitPoints);
        Assert.Contains(claudeMinion, session1.Enemies);
        Assert.Equal(0f, session1.InvulnerableSecondsLeft);

        // Case 2: Once the Claude minion lands overlapping the ship, touching costs a hit point and removes it
        // Drive movement until HasLanded is true
        while (!thrownMovement.HasLanded)
        {
            claudeMinion.Update(shipPos, 0.1f);
        }
        Assert.True(thrownMovement.HasLanded);
        Assert.False(thrownMovement.IsHarmlessInFlight);

        // Place a landed Claude at the ship's position
        var session2 = TestEnemies.QuietSession(shipPos);
        session2.Stats.MaxHitPoints = session2.Stats.HitPoints = 3;
        var landedClaude = new Enemy(EnemyRoster.Claude, shipPos, claudeMinion.Movement);
        session2.AddEnemy(landedClaude);

        Assert.Equal(3, session2.Stats.HitPoints);
        session2.Update(Vector2.Zero, null, 0.01f, 1000f, 800f);

        Assert.Equal(2, session2.Stats.HitPoints);
        Assert.DoesNotContain(landedClaude, session2.Enemies);
        Assert.Equal(GameSession.InvulnerableSeconds, session2.InvulnerableSecondsLeft);

        // Case 3: Discriminating case - an airborne GeminiBoss-thrown Gemini still hurts on contact
        var session3 = TestEnemies.QuietSession(shipPos);
        session3.Stats.MaxHitPoints = session3.Stats.HitPoints = 3;
        var geminiBoss = new Enemy(EnemyRoster.GeminiBoss, shipPos);
        var geminiMinions = geminiBoss.UseAbility(shipPos, 3.0f, new Random(456));
        Assert.NotEmpty(geminiMinions);
        var geminiMinion = geminiMinions[0];
        Assert.Same(EnemyRoster.Gemini, geminiMinion.Type);
        var geminiThrown = Assert.IsType<Thrown>(geminiMinion.Movement);
        Assert.False(geminiThrown.IsHarmlessInFlight);
        Assert.False(geminiThrown.HasLanded);

        session3.AddEnemy(geminiMinion);
        Assert.Equal(3, session3.Stats.HitPoints);
        session3.Update(Vector2.Zero, null, 0.01f, 1000f, 800f);

        // Airborne Gemini costs a hit point and is removed
        Assert.Equal(2, session3.Stats.HitPoints);
        Assert.DoesNotContain(geminiMinion, session3.Enemies);
        Assert.True(session3.InvulnerableSecondsLeft > 0f);
    }

    [Fact]
    public void ClaudeBoss_thrown_claude_does_not_lunge_while_airborne_and_creeps_after_landing()
    {
        // CLM-2: A Claude thrown by ClaudeBoss does not lunge while airborne.
        // After it lands, its Dash starts in creep and creeps for at least 1.0 s
        // (the Claude type's 1.6 s creepSeconds) before its first lunge and before every later lunge.
        // Its creep clock runs only from landing, never during flight.
        // Proof obligation: A new test drives a Claude Boss-thrown Claude through Enemy.Update until it lands.
        // It records the time each lunge starts (the per-frame displacement jumps to the 420 px/s dash speed)
        // and asserts the first lunge starts >= 1.0 s after landing and every later lunge >= 1.0 s
        // after the previous lunge ended.
        var boss = new Enemy(EnemyRoster.ClaudeBoss, BossCenter);
        boss.TakeDamage(boss.Type.HitPoints * 0.75f);
        var rng = new Random(789);
        var minions = boss.UseAbility(PlayerPosition, 2.0f, rng);
        Assert.NotEmpty(minions);
        var claude = minions[0];
        var thrown = Assert.IsType<Thrown>(claude.Movement);

        const float dt = 0.02f;
        var flightTime = 0f;

        // Drive while in flight, verifying no lunge occurs
        while (!thrown.HasLanded)
        {
            var prevPos = claude.Position;
            claude.Update(PlayerPosition, dt);
            flightTime += dt;
            var displacement = Vector2.Distance(prevPos, claude.Position);
            var speed = displacement / dt;

            // Flight speed is Claude.Speed * 4 = 60 * 4 = 240 px/s, well below the 420 px/s dash speed
            Assert.True(speed <= 250f, $"Speed while airborne was {speed}, expected <= 250 px/s (not lunging)");
        }

        // Now that it has landed, drive after landing and record lunge start times and end times
        var timeSinceLanding = 0f;
        var lungeStarts = new List<float>();
        var lungeEnds = new List<float>();
        var isLunging = false;

        // Run for 10 seconds post-landing
        for (var step = 0; step < 500; step++)
        {
            var prevPos = claude.Position;
            claude.Update(PlayerPosition, dt);
            timeSinceLanding += dt;
            var displacement = Vector2.Distance(prevPos, claude.Position);
            var speed = displacement / dt;

            var dashingThisFrame = speed > 350f; // 420 px/s dash speed
            if (dashingThisFrame && !isLunging)
            {
                isLunging = true;
                lungeStarts.Add(timeSinceLanding);
            }
            else if (!dashingThisFrame && isLunging)
            {
                isLunging = false;
                lungeEnds.Add(timeSinceLanding);
            }
        }

        // Assert we observed lunges
        Assert.True(lungeStarts.Count >= 2, $"Expected at least 2 lunges, observed {lungeStarts.Count}");

        // First lunge starts >= 1.0 s after landing
        Assert.True(lungeStarts[0] >= 1.0f, $"First lunge started at {lungeStarts[0]}s, expected >= 1.0s after landing");
        // Claude's creepSeconds is 1.6s, so around 1.6s
        Assert.InRange(lungeStarts[0], 1.55f, 1.7f);

        // Every later lunge starts >= 1.0 s after previous lunge ended
        for (var i = 1; i < lungeStarts.Count; i++)
        {
            var prevEnd = lungeEnds[i - 1];
            var interval = lungeStarts[i] - prevEnd;
            Assert.True(interval >= 1.0f, $"Lunge {i} started {interval}s after previous lunge ended, expected >= 1.0s");
            Assert.InRange(interval, 1.55f, 1.7f);
        }
    }
}
