using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>One drawn stretch of a lightning strike: from the ship to its first target, or from one target to the next.</summary>
public readonly record struct LightningSegment(Vector2 From, Vector2 To);

/// <summary>
/// The lightning weapon: each strike hits the live enemy nearest the ship, then chains to a random live enemy within
/// ChainRange of the one just struck (earlier targets allowed, the one just struck never), each hop dealing
/// HopDamageFactor times the previous hit. The chain stops after its hops, or early when no other live enemy is in range.
/// </summary>
public static class Lightning
{
    /// <summary>How far a chain can jump from the enemy just struck, in pixels.</summary>
    public const float ChainRange = 150f;

    /// <summary>Each hop deals this fraction of the hit before it.</summary>
    public const float HopDamageFactor = 0.8f;

    /// <summary>From this weapon level every enemy struck also burns (see Enemy.ApplyBurn).</summary>
    public const int BurnLevel = 5;

    /// <summary>True when there is a live enemy to strike.</summary>
    public static bool HasTarget(IReadOnlyList<Enemy> enemies)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Makes one strike from origin and appends its segments. Every hit goes through dealDamage, in order, so an enemy
    /// a hit kills is no longer a live target for the hops after it.
    /// </summary>
    /// <param name="damage">Damage of the first hit.</param>
    /// <param name="hops">How many times the strike may chain after its first hit.</param>
    /// <param name="level">The weapon's level; at BurnLevel every enemy struck burns.</param>
    /// <param name="random">Picks each hop's target among those in range.</param>
    /// <param name="dealDamage">Deals one hit's damage to one enemy (the session's damage-and-credit helper).</param>
    public static void Strike(Vector2 origin, float damage, int hops, int level, IReadOnlyList<Enemy> enemies,
        Random random, Action<Enemy, float> dealDamage, List<LightningSegment> segments)
    {
        var target = Nearest(origin, enemies);
        if (target is null)
            return;

        segments.Add(new LightningSegment(origin, target.Position));
        Hit(target, damage, level, dealDamage);

        var inRange = new List<Enemy>();
        for (var hop = 0; hop < hops; hop++)
        {
            inRange.Clear();
            foreach (var enemy in enemies)
            {
                if (!enemy.IsDead && !ReferenceEquals(enemy, target)
                    && Vector2.Distance(enemy.Position, target.Position) <= ChainRange)
                    inRange.Add(enemy);
            }
            if (inRange.Count == 0)
                return;

            var next = inRange[random.Next(inRange.Count)];
            damage *= HopDamageFactor;
            segments.Add(new LightningSegment(target.Position, next.Position));
            Hit(next, damage, level, dealDamage);
            target = next;
        }
    }

    private static void Hit(Enemy enemy, float damage, int level, Action<Enemy, float> dealDamage)
    {
        dealDamage(enemy, damage);
        if (level >= BurnLevel)
            enemy.ApplyBurn();
    }

    private static Enemy? Nearest(Vector2 point, IReadOnlyList<Enemy> enemies)
    {
        Enemy? nearest = null;
        var best = float.PositiveInfinity;
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead)
                continue;
            var distance = Vector2.DistanceSquared(point, enemy.Position);
            if (distance < best)
                (nearest, best) = (enemy, distance);
        }
        return nearest;
    }
}
