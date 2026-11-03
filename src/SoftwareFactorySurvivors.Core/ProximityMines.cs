using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>One live mine: where it lies, how long it has lain there, and its blast, fixed when it was dropped.</summary>
public sealed class Mine
{
    internal Mine(Vector2 position, float damage, float blastRadius)
    {
        Position = position;
        Damage = damage;
        BlastRadius = blastRadius;
    }

    public Vector2 Position { get; }
    public float Damage { get; }
    public float BlastRadius { get; }

    /// <summary>Seconds since it was dropped.</summary>
    public float AgeSeconds { get; internal set; }

    /// <summary>True once it has lain ProximityMines.ArmSeconds; before that an enemy touching it sets nothing off.</summary>
    public bool IsArmed => AgeSeconds >= ProximityMines.ArmSeconds - ProximityMines.TimeSlack;
}

/// <summary>
/// The mines in play for the Proximity Mines weapon. A dropped mine is harmless for ArmSeconds, then explodes the
/// moment an enemy touches it, damaging every live enemy whose centre is within its blast radius (inclusive) once.
/// A mine nothing touches is gone after LifetimeSeconds, and at most MaxLiveMines lie at once: a drop past that
/// removes the oldest.
/// </summary>
public sealed class ProximityMines
{
    public const float ArmSeconds = 0.5f;
    public const float LifetimeSeconds = 10f;
    public const int MaxLiveMines = 12;

    /// <summary>Drawn and touch-tested radius of a mine.</summary>
    public const float MineRadius = 8f;

    // Absorbs float drift in a timer summed from many small frames, so a mine due at 0.5 s isn't a frame late.
    internal const float TimeSlack = 1e-4f;

    private readonly List<Mine> _mines = [];
    private readonly List<Circle> _explosions = [];
    private readonly List<Enemy> _caught = [];

    /// <summary>Mines lying in play, oldest first.</summary>
    public IReadOnlyList<Mine> Mines => _mines;

    /// <summary>Every mine explosion of the last Update: where, and how far it reached.</summary>
    public IReadOnlyList<Circle> Explosions => _explosions;

    /// <summary>Lays one mine at each position with the weapon's damage and blast as they are now.</summary>
    public void Drop(OwnedWeapon weapon, IReadOnlyList<Vector2> positions)
    {
        foreach (var position in positions)
        {
            _mines.Add(new Mine(position, weapon.Damage, weapon.BlastRadius));
            if (_mines.Count > MaxLiveMines)
                _mines.RemoveAt(0);
        }
    }

    /// <summary>
    /// Ages every mine and sets off the armed mines an enemy touches. Mines past their lifetime are removed unexploded. All damage goes through dealDamage.
    /// </summary>
    public void Update(float elapsedSeconds, IReadOnlyList<Enemy> enemies, Action<Enemy, float> dealDamage)
    {
        _explosions.Clear();

        _mines.RemoveAll(mine =>
        {
            mine.AgeSeconds += elapsedSeconds;
            if (mine.AgeSeconds >= LifetimeSeconds - TimeSlack)
                return true;
            if (!mine.IsArmed || !IsTouched(mine, enemies))
                return false;

            Explode(mine.Position, mine.BlastRadius, mine.Damage, enemies, dealDamage);
            return true;
        });
    }

    private static bool IsTouched(Mine mine, IReadOnlyList<Enemy> enemies)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead && RicochetDisc.Touches(mine.Position, MineRadius, enemy))
                return true;
        }
        return false;
    }

    private void Explode(Vector2 center, float radius, float damage, IReadOnlyList<Enemy> enemies, Action<Enemy, float> dealDamage)
    {
        _explosions.Add(new Circle(center, radius));
        // Collect first, so an enemy a hit adds or removes doesn't change who this blast reaches.
        _caught.Clear();
        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead && Vector2.Distance(center, enemy.Position) <= radius)
                _caught.Add(enemy);
        }
        // dealDamage credits a kill only when this hit is what killed the enemy, so overlapping blasts credit it once.
        foreach (var enemy in _caught)
            dealDamage(enemy, damage);
    }
}
