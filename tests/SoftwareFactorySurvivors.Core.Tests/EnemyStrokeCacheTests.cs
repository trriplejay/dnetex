using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class EnemyStrokeCacheTests
{
    private static readonly Vector2 Start = new(320, 240);

    [Fact]
    public void Repeated_calls_without_a_change_return_the_same_outline_without_allocating()
    {
        var enemy = TestEnemies.Stationary(EnemyRoster.Claude, Start);
        var first = enemy.GetStrokes();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var second = enemy.GetStrokes();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Same(first, second);
        Assert.Equal(0L, allocated);
        AssertMatchesFresh(enemy, second);
    }

    [Fact]
    public void Moving_enemy_gets_a_fresh_outline_after_update()
    {
        var enemy = EnemyRoster.Gemini.Create(Start);
        var before = enemy.GetStrokes();

        enemy.Update(new Vector2(0, 0), 0.5f);
        var after = enemy.GetStrokes();

        Assert.NotEqual(Start, enemy.Position);
        Assert.NotSame(before, after);
        AssertMatchesFresh(enemy, after);
        Assert.Same(after, enemy.GetStrokes());
    }

    [Fact]
    public void Spinning_enemy_gets_a_fresh_outline_after_update_and_keeps_the_old_one_intact()
    {
        var enemy = TestEnemies.Stationary(EnemyRoster.Claude, Start);
        var before = enemy.GetStrokes();
        var beforeCopy = before.Select(stroke => stroke.ToArray()).ToArray();

        enemy.Update(Vector2.Zero, 0.25f);
        var after = enemy.GetStrokes();

        Assert.Equal(Start, enemy.Position);
        Assert.NotEqual(0f, enemy.Rotation);
        AssertMatchesFresh(enemy, after);
        // An outline a caller already holds is never rewritten in place.
        Assert.Equal(beforeCopy, before);
    }

    [Fact]
    public void Phase_change_from_damage_keeps_the_outline_equal_to_a_fresh_one_before_and_after_moving()
    {
        var type = EnemyRoster.Gemini with
        {
            Phases = [new BossPhase(0.75f, CreateMovement: () => new ChasePlayer(200f))],
        };
        var enemy = new Enemy(type, Start, new Hold());
        enemy.Update(Vector2.Zero, 0.1f);
        var held = enemy.GetStrokes();
        Assert.Equal(Start, enemy.Position);

        enemy.TakeDamage(type.HitPoints * 0.5f);
        Assert.Equal(1, enemy.PhasesReached);
        AssertMatchesFresh(enemy, enemy.GetStrokes());

        // The new phase's movement takes over; the cached outline follows the enemy.
        enemy.Update(Vector2.Zero, 0.5f);
        var moved = enemy.GetStrokes();
        Assert.NotEqual(Start, enemy.Position);
        Assert.NotSame(held, moved);
        AssertMatchesFresh(enemy, moved);
    }

    private static void AssertMatchesFresh(Enemy enemy, IReadOnlyList<Vector2[]> actual)
    {
        var transform = Matrix3x2.CreateRotation(enemy.Rotation) * Matrix3x2.CreateTranslation(enemy.Position);
        var expected = enemy.Type.Outline.Select(stroke => stroke.Select(p => Vector2.Transform(p, transform)).ToArray()).ToArray();
        Assert.Equal(expected, actual);
    }

    private sealed class Hold : IMovementPattern
    {
        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) => position;
    }
}
