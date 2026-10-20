using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class AutocompleteBulletRingTests
{
    private const float Width = 3000f;
    private const float Height = 1000f;
    private const float Dt = 0.1f;

    [Fact]
    public void Autocomplete_roster_phase_configuration_for_abilities()
    {
        // (a) roster lookup by AtHealthFraction 0.50 has non-null CreateAbility returning BulletBursts and 0.25 has null CreateAbility
        var phases = EnemyRoster.Autocomplete.Phases;
        Assert.NotNull(phases);

        var phase50 = Assert.Single(phases, p => MathF.Abs(p.AtHealthFraction - 0.50f) < 0.001f);
        Assert.NotNull(phase50.CreateAbility);
        var ability50 = phase50.CreateAbility();
        var bursts = Assert.IsType<BulletBursts>(ability50);
        Assert.Equal(BulletBursts.BulletSpeed, bursts.Speed);

        var phase25 = Assert.Single(phases, p => MathF.Abs(p.AtHealthFraction - 0.25f) < 0.001f);
        Assert.Null(phase25.CreateAbility);

        var phase75 = Assert.Single(phases, p => MathF.Abs(p.AtHealthFraction - 0.75f) < 0.001f);
        Assert.Null(phase75.CreateAbility);

        // Order stays [0.75, 0.50, 0.25]
        Assert.Equal([0.75f, 0.50f, 0.25f], phases.Select(p => p.AtHealthFraction).ToArray());
    }

    [Fact]
    public void Autocomplete_game_session_emits_bullet_hazards_after_50_percent_crossing()
    {
        // (b) a GameSession with an Autocomplete boss damaged below 50% yields HazardKind.Bullet hazards
        // in session.Hazards after ~3 s, and none before the crossing or within the first 3.0 s after it;
        // The ship is far from the boss's charges so contact never ends the session mid-test.
        var shipPos = new Vector2(2900f, 500f);
        var session = TestEnemies.QuietSession(shipPos);
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 500f));
        session.AddEnemy(boss);

        // Before 50% (damage to 60%, 36 HP)
        boss.TakeDamage(24f);
        Assert.Equal(36f, boss.HitPoints);
        Assert.Equal(1, boss.PhasesReached);
        Assert.Null(boss.Ability);

        // Run for 4 seconds - no bullets should ever be emitted
        for (var i = 0; i < 40; i++)
        {
            session.Update(Vector2.Zero, null, Dt, Width, Height);
            Assert.DoesNotContain(session.Hazards, h => h.Kind == HazardKind.Bullet);
        }
        Assert.False(session.IsFinished);

        // Damage below 50% threshold (50% of 60 = 30 HP; damage 10f -> 26 HP)
        boss.TakeDamage(10f);
        Assert.Equal(26f, boss.HitPoints);
        Assert.Equal(2, boss.PhasesReached);
        Assert.NotNull(boss.Ability);
        var burstAbility = Assert.IsType<BulletBursts>(boss.Ability);

        // First burst comes after 3.0 s of updates, NOT on threshold frame or within the first 3.0s (e.g. 2.9s)
        for (var i = 0; i < 29; i++)
        {
            session.Update(Vector2.Zero, null, Dt, Width, Height);
            Assert.DoesNotContain(session.Hazards, h => h.Kind == HazardKind.Bullet);
        }

        // Advance past 3.0s (e.g. two more steps: 3.0s, 3.1s)
        session.Update(Vector2.Zero, null, Dt, Width, Height);
        session.Update(Vector2.Zero, null, Dt, Width, Height);

        var bullets = session.Hazards.Where(h => h.Kind == HazardKind.Bullet).ToList();
        Assert.NotEmpty(bullets);
        Assert.Equal(12, bullets.Count);
    }

    [Fact]
    public void Autocomplete_crossing_25_percent_keeps_same_bullet_bursts_instance()
    {
        // (c) after a further crossing of 0.25, boss.Ability is ReferenceEquals to the BulletBursts from 0.50
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(500f, 500f));

        // Damage below 50% (e.g. 25 HP)
        boss.TakeDamage(35f);
        Assert.Equal(2, boss.PhasesReached);
        var abilityAt50 = boss.Ability;
        Assert.NotNull(abilityAt50);
        Assert.IsType<BulletBursts>(abilityAt50);

        // Damage below 25% (e.g. 10 HP)
        boss.TakeDamage(15f);
        Assert.Equal(3, boss.PhasesReached);
        Assert.Same(abilityAt50, boss.Ability);
    }

    [Fact]
    public void Autocomplete_single_hit_full_health_to_10_percent_grants_bullet_bursts()
    {
        // (d) one TakeDamage from full health to ~10% gives PhasesReached 3 with Ability a BulletBursts
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(500f, 500f));
        Assert.Null(boss.Ability);

        boss.TakeDamage(54f);
        Assert.Equal(6f, boss.HitPoints);
        Assert.False(boss.IsDead);
        Assert.Equal(3, boss.PhasesReached);
        Assert.NotNull(boss.Ability);
        Assert.IsType<BulletBursts>(boss.Ability);
    }

    [Fact]
    public void Autocomplete_killing_blow_leaves_phases_zero_and_ability_null()
    {
        // (e) a killing blow leaves PhasesReached 0 and Ability null
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(500f, 500f));
        Assert.Null(boss.Ability);

        boss.TakeDamage(60f);
        Assert.True(boss.IsDead);
        Assert.Equal(0, boss.PhasesReached);
        Assert.Null(boss.Ability);
    }

    [Fact]
    public void Autocomplete_burst_fairness_at_phases_2_and_3()
    {
        // CLM-4 fairness obligation: bosses at PhasesReached 2 and 3, seeded Randoms,
        // every bullet speed < Ship.Speed and largest angular gap > MinRequiredGapRadians.
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(500f, 500f));
        boss.TakeDamage(35f); // PhasesReached 2
        Assert.Equal(2, boss.PhasesReached);
        var ability = Assert.IsType<BulletBursts>(boss.Ability);

        int[] seeds = [1, 42, 999, 12345];
        foreach (var seed in seeds)
        {
            var rng = new Random(seed);
            // Drive ability update for 3.0s to trigger a burst
            ability.Update(boss, new Vector2(100f, 100f), 3.0f, rng);
            var hazards = ability.TakeHazards();
            var bullets = hazards.Where(h => h.Kind == HazardKind.Bullet).ToList();
            Assert.Equal(12, bullets.Count);

            // Extract bullet velocity and direction by stepping dt=0.1
            var angles = new List<float>();
            foreach (var hazard in bullets)
            {
                var initialCircle = Assert.Single(hazard.Shape.Circles);
                var initialPos = initialCircle.Center;
                hazard.Update(0.1f);
                var nextCircle = Assert.Single(hazard.Shape.Circles);
                var velocity = (nextCircle.Center - initialPos) / 0.1f;
                var speed = velocity.Length();

                Assert.True(speed < Ship.Speed, $"Bullet speed {speed} must be strictly less than Ship.Speed {Ship.Speed}");

                var angle = MathF.Atan2(velocity.Y, velocity.X);
                angle = (angle % MathF.Tau + MathF.Tau) % MathF.Tau;
                angles.Add(angle);
            }

            angles.Sort();

            // Find maximum angular gap between consecutive bullets
            var maxGap = 0f;
            for (var i = 0; i < angles.Count; i++)
            {
                var current = angles[i];
                var next = (i == angles.Count - 1) ? angles[0] + MathF.Tau : angles[i + 1];
                var gap = next - current;
                if (gap > maxGap)
                    maxGap = gap;
            }

            Assert.True(maxGap > BulletBursts.MinRequiredGapRadians,
                $"Max gap {maxGap} must be > {BulletBursts.MinRequiredGapRadians}");
        }

        // Also test at PhasesReached 3
        boss.TakeDamage(15f); // PhasesReached 3
        Assert.Equal(3, boss.PhasesReached);
        Assert.Same(ability, boss.Ability);

        foreach (var seed in seeds)
        {
            var rng = new Random(seed + 100);
            ability.Update(boss, new Vector2(100f, 100f), 3.0f, rng);
            var hazards = ability.TakeHazards();
            var bullets = hazards.Where(h => h.Kind == HazardKind.Bullet).ToList();
            Assert.Equal(12, bullets.Count);

            var angles = new List<float>();
            foreach (var hazard in bullets)
            {
                var initialCircle = Assert.Single(hazard.Shape.Circles);
                var initialPos = initialCircle.Center;
                hazard.Update(0.1f);
                var nextCircle = Assert.Single(hazard.Shape.Circles);
                var velocity = (nextCircle.Center - initialPos) / 0.1f;
                var speed = velocity.Length();

                Assert.True(speed < Ship.Speed, $"Bullet speed {speed} must be strictly less than Ship.Speed {Ship.Speed}");

                var angle = MathF.Atan2(velocity.Y, velocity.X);
                angle = (angle % MathF.Tau + MathF.Tau) % MathF.Tau;
                angles.Add(angle);
            }

            angles.Sort();

            var maxGap = 0f;
            for (var i = 0; i < angles.Count; i++)
            {
                var current = angles[i];
                var next = (i == angles.Count - 1) ? angles[0] + MathF.Tau : angles[i + 1];
                var gap = next - current;
                if (gap > maxGap)
                    maxGap = gap;
            }

            Assert.True(maxGap > BulletBursts.MinRequiredGapRadians,
                $"Max gap {maxGap} must be > {BulletBursts.MinRequiredGapRadians}");
        }
    }

    [Fact]
    public void Autocomplete_bullets_remain_after_boss_death_until_lifetime_expires()
    {
        // CLM-1: In a GameSession with an Autocomplete boss past its 0.50 Phases entry,
        // HazardKind.Bullet hazards are not removed when the boss dies: every one still present
        // on the Update the boss is killed stays in session.Hazards until its AgeSeconds reaches
        // its LifetimeSeconds (4.0 s for Hazard.CreateBullet), and only then is it removed.
        var shipPos = new Vector2(2900f, 500f);
        var session = new GameSession(shipPos, new Random(1), roster: [],
            bosses: [new BossArrival(0f, EnemyRoster.Autocomplete)], rings: []);

        // Initial update spawns the boss
        session.Update(Vector2.Zero, null, Dt, Width, Height);
        var boss = Assert.Single(session.Bosses);
        Assert.Contains(boss, session.Enemies);

        // Bring Autocomplete past 50% threshold (e.g. 26 HP remaining)
        boss.TakeDamage(34f);
        Assert.Equal(2, boss.PhasesReached);
        Assert.NotNull(boss.Ability);

        // Advance updates until Bullet hazards appear
        for (var i = 0; i < 40; i++)
        {
            session.Update(Vector2.Zero, null, Dt, Width, Height);
            if (session.Hazards.Any(h => h.Kind == HazardKind.Bullet))
                break;
        }

        var snapshottedBullets = session.Hazards.Where(h => h.Kind == HazardKind.Bullet).ToList();
        Assert.NotEmpty(snapshottedBullets);
        Assert.Equal(12, snapshottedBullets.Count);
        Assert.All(snapshottedBullets, b => Assert.True(b.AgeSeconds < b.LifetimeSeconds));

        // Kill the boss
        boss.TakeDamage(boss.HitPoints);
        Assert.True(boss.IsDead);

        // On the next Update, boss is removed, but bullets stay
        session.Update(Vector2.Zero, null, Dt, Width, Height);
        Assert.DoesNotContain(boss, session.Enemies);
        Assert.DoesNotContain(boss, session.Bosses);

        foreach (var bullet in snapshottedBullets)
        {
            Assert.Contains(session.Hazards, h => ReferenceEquals(h, bullet));
        }

        // Keep stepping: assert each snapshotted bullet is present while AgeSeconds < LifetimeSeconds
        // and gone once its lifetime has passed.
        // Step in increments until all snapshotted bullets expire
        while (snapshottedBullets.Any(b => b.AgeSeconds < b.LifetimeSeconds))
        {
            foreach (var bullet in snapshottedBullets)
            {
                if (bullet.AgeSeconds < bullet.LifetimeSeconds)
                {
                    Assert.Contains(session.Hazards, h => ReferenceEquals(h, bullet));
                }
                else
                {
                    Assert.DoesNotContain(session.Hazards, h => ReferenceEquals(h, bullet));
                }
            }

            session.Update(Vector2.Zero, null, Dt, Width, Height);
        }

        // After all snapshotted bullets have passed lifetime and been updated:
        foreach (var bullet in snapshottedBullets)
        {
            Assert.True(bullet.AgeSeconds >= bullet.LifetimeSeconds);
            Assert.DoesNotContain(session.Hazards, h => ReferenceEquals(h, bullet));
        }
    }
}
