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
        // In quiet session, ship fires 1 attack per second up from nose
        for (var i = 0; i < 60 && !copy.IsDead; i++)
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
        var session = TestEnemies.QuietSession(new Vector2(900, 700));
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

    private sealed class StationaryPattern : IMovementPattern
    {
        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) =>
            position;

        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds, float speedFactor) =>
            position;
    }
}
