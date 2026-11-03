using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// One thrown Ricochet Disc. It flies straight out from where it was thrown until it has covered its range, turns
/// back at that far point and flies to the ship (following it as it moves) until it is caught. It cuts through
/// every enemy it overlaps, hitting each one at most once per leg (out, back).
/// </summary>
public sealed class RicochetDisc
{
    /// <summary>Disc radius at size scale 1, in pixels.</summary>
    public const float BaseRadius = 12f;

    /// <summary>Angle between neighbouring discs when one throw sends several.</summary>
    public const float FanSpreadRadians = 20f * MathF.PI / 180f;

    // Absorbs float drift in a distance summed from many small frames, so the turn isn't a frame late.
    private const float RangeSlack = 1e-3f;

    private readonly HashSet<Enemy> _hitThisLeg = new(ReferenceEqualityComparer.Instance);
    private readonly Vector2 _origin;
    private float _travelled;

    private RicochetDisc(Vector2 origin, Vector2 direction, float damage, float radius, float range, float speed)
    {
        _origin = origin;
        Position = origin;
        Direction = direction;
        Damage = damage;
        Radius = radius;
        Range = range;
        Speed = speed;
    }

    public Vector2 Position { get; private set; }

    /// <summary>The unit direction the disc was thrown along.</summary>
    public Vector2 Direction { get; }

    /// <summary>Damage per hit, fixed when the disc was thrown.</summary>
    public float Damage { get; }

    /// <summary>Drawn and hit-tested radius.</summary>
    public float Radius { get; }

    public float Range { get; }
    public float Speed { get; }

    /// <summary>True once the disc has turned at its far point and is flying back to the ship.</summary>
    public bool IsReturning { get; private set; }

    /// <summary>True once the returning disc has reached the ship; it is then gone.</summary>
    public bool IsCaught { get; private set; }

    /// <summary>
    /// The discs one throw of the weapon sends: Count of them, fanned around the direction to the nearest live
    /// enemy, or around forward when no enemy is alive. Each carries the weapon's damage, size, range and speed as
    /// they are now.
    /// </summary>
    public static IReadOnlyList<RicochetDisc> Throw(OwnedWeapon weapon, Vector2 origin, Vector2 forward, IReadOnlyList<Enemy> enemies)
    {
        var aim = NearestDirection(origin, enemies) ?? forward;
        var heading = MathF.Atan2(aim.Y, aim.X);
        var count = Math.Max(0, weapon.Count);
        var discs = new RicochetDisc[count];
        for (var i = 0; i < count; i++)
        {
            var angle = heading + (i - (count - 1) / 2f) * FanSpreadRadians;
            discs[i] = new RicochetDisc(origin, new Vector2(MathF.Cos(angle), MathF.Sin(angle)), weapon.Damage,
                BaseRadius * weapon.SizeScale, weapon.Range, weapon.Speed);
        }
        return discs;
    }

    private static Vector2? NearestDirection(Vector2 origin, IReadOnlyList<Enemy> enemies)
    {
        Enemy? nearest = null;
        var best = float.PositiveInfinity;
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead)
                continue;
            var distance = Vector2.DistanceSquared(origin, enemy.Position);
            if (distance < best)
            {
                best = distance;
                nearest = enemy;
            }
        }
        if (nearest is null || best <= 0f)
            return null;
        return Vector2.Normalize(nearest.Position - origin);
    }

    /// <summary>
    /// Moves the disc: out along its direction until it has covered its range, then back toward shipPosition. On
    /// the turn it forgets the enemies it hit on the way out.
    /// </summary>
    public void Update(float elapsedSeconds, Vector2 shipPosition)
    {
        if (IsCaught)
            return;

        var step = Speed * elapsedSeconds;
        if (!IsReturning)
        {
            if (_travelled + step < Range - RangeSlack)
            {
                _travelled += step;
                Position = _origin + Direction * _travelled;
                return;
            }

            // The far point: turn back, and spend what is left of this frame's step on the way home.
            step -= MathF.Max(0f, Range - _travelled);
            _travelled = Range;
            Position = _origin + Direction * Range;
            IsReturning = true;
            _hitThisLeg.Clear();
        }

        var toShip = shipPosition - Position;
        var distance = toShip.Length();
        if (distance <= step)
        {
            Position = shipPosition;
            IsCaught = true;
            return;
        }
        Position += toShip / distance * step;
    }

    /// <summary>Hits every live enemy the disc overlaps that it hasn't hit on this leg yet.</summary>
    public void Strike(IReadOnlyList<Enemy> enemies, Action<Enemy, float> dealDamage)
    {
        if (IsCaught)
            return;
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || _hitThisLeg.Contains(enemy) || !Touches(Position, Radius, enemy))
                continue;
            _hitThisLeg.Add(enemy);
            dealDamage(enemy, Damage);
        }
    }

    /// <summary>True when a disc of the given radius at center touches the enemy's outline.</summary>
    internal static bool Touches(Vector2 center, float radius, Enemy enemy)
    {
        // The bounding circles only skip enemies that provably can't be reached, before the exact test.
        var reach = radius + enemy.Type.Radius;
        return Vector2.DistanceSquared(center, enemy.Position) <= reach * reach
            && Collision.CircleTouches(center, radius, enemy.GetStrokes());
    }
}
