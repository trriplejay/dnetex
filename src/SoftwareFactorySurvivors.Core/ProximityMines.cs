using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>One live mine: where it lies, how long it has lain there, and its blast, fixed when it was dropped.</summary>
public sealed class Mine
{
    internal Mine(Vector2 position, float damage, float blastRadius, bool cluster)
    {
        Position = position;
        Damage = damage;
        BlastRadius = blastRadius;
        Cluster = cluster;
    }

    public Vector2 Position { get; }
    public float Damage { get; }
    public float BlastRadius { get; }

    /// <summary>True when it was dropped at the weapon's level 5 (CLUSTER): its explosion throws mini-mines.</summary>
    public bool Cluster { get; }

    /// <summary>Seconds since it was dropped.</summary>
    public float AgeSeconds { get; internal set; }

    /// <summary>True once it has lain ProximityMines.ArmSeconds; before that an enemy touching it sets nothing off.</summary>
    public bool IsArmed => AgeSeconds >= ProximityMines.ArmSeconds - ProximityMines.TimeSlack;
}

/// <summary>A CLUSTER mini-mine: thrown out by a mine's explosion, it goes off on its own once its fuse runs out.</summary>
public sealed class MiniMine
{
    internal MiniMine(Vector2 position, float damage, float blastRadius)
    {
        Position = position;
        Damage = damage;
        BlastRadius = blastRadius;
        FuseSecondsLeft = ProximityMines.MiniMineFuseSeconds;
    }

    public Vector2 Position { get; }

    /// <summary>Half the damage of the mine that threw it.</summary>
    public float Damage { get; }

    /// <summary>Half the blast radius of the mine that threw it.</summary>
    public float BlastRadius { get; }

    public float FuseSecondsLeft { get; internal set; }
}

/// <summary>
/// The mines in play for the Proximity Mines weapon. A dropped mine is harmless for ArmSeconds, then explodes the
/// moment an enemy touches it, damaging every live enemy whose centre is within its blast radius (inclusive) once.
/// A mine nothing touches is gone after LifetimeSeconds, and at most MaxLiveMines lie at once: a drop past that
/// removes the oldest. A mine dropped at level 5 (CLUSTER) throws MiniMinesPerBlast mini-mines MiniMineThrowDistance
/// out when it explodes; each goes off MiniMineFuseSeconds later at half the damage and half the radius, and throws
/// nothing itself.
/// </summary>
public sealed class ProximityMines
{
    public const float ArmSeconds = 0.5f;
    public const float LifetimeSeconds = 10f;
    public const int MaxLiveMines = 12;

    /// <summary>Drawn and touch-tested radius of a mine.</summary>
    public const float MineRadius = 8f;

    public const int MiniMinesPerBlast = 3;
    public const float MiniMineThrowDistance = 50f;
    public const float MiniMineFuseSeconds = 0.3f;

    // Absorbs float drift in a timer summed from many small frames, so a mine due at 0.5 s isn't a frame late.
    internal const float TimeSlack = 1e-4f;

    private readonly List<Mine> _mines = [];
    private readonly List<MiniMine> _miniMines = [];
    private readonly List<Circle> _explosions = [];
    private readonly List<Enemy> _caught = [];

    /// <summary>Mines lying in play, oldest first.</summary>
    public IReadOnlyList<Mine> Mines => _mines;

    /// <summary>CLUSTER mini-mines waiting for their fuse.</summary>
    public IReadOnlyList<MiniMine> MiniMines => _miniMines;

    /// <summary>Every mine and mini-mine explosion of the last Update: where, and how far it reached.</summary>
    public IReadOnlyList<Circle> Explosions => _explosions;

    /// <summary>Lays one mine at each position with the weapon's damage and blast as they are now.</summary>
    public void Drop(OwnedWeapon weapon, IReadOnlyList<Vector2> positions)
    {
        foreach (var position in positions)
        {
            _mines.Add(new Mine(position, weapon.Damage, weapon.BlastRadius, weapon.IsFinale));
            if (_mines.Count > MaxLiveMines)
                _mines.RemoveAt(0);
        }
    }

    /// <summary>
    /// Ages every mine and mini-mine and sets off what is due: mini-mines whose fuse ran out, then armed mines an
    /// enemy touches. Mines past their lifetime are removed unexploded. All damage goes through dealDamage.
    /// </summary>
    public void Update(float elapsedSeconds, IReadOnlyList<Enemy> enemies, Action<Enemy, float> dealDamage)
    {
        _explosions.Clear();

        // Mini-mines first, so those thrown this frame wait their whole fuse.
        for (var i = 0; i < _miniMines.Count; i++)
        {
            var mini = _miniMines[i];
            mini.FuseSecondsLeft -= elapsedSeconds;
            if (mini.FuseSecondsLeft <= TimeSlack)
                Explode(mini.Position, mini.BlastRadius, mini.Damage, enemies, dealDamage);
        }
        _miniMines.RemoveAll(mini => mini.FuseSecondsLeft <= TimeSlack);

        _mines.RemoveAll(mine =>
        {
            mine.AgeSeconds += elapsedSeconds;
            if (mine.AgeSeconds >= LifetimeSeconds - TimeSlack)
                return true;
            if (!mine.IsArmed || !IsTouched(mine, enemies))
                return false;

            Explode(mine.Position, mine.BlastRadius, mine.Damage, enemies, dealDamage);
            if (mine.Cluster)
                ThrowMiniMines(mine);
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

    private void ThrowMiniMines(Mine mine)
    {
        for (var i = 0; i < MiniMinesPerBlast; i++)
        {
            var angle = i * MathF.Tau / MiniMinesPerBlast;
            var offset = MiniMineThrowDistance * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _miniMines.Add(new MiniMine(mine.Position + offset, mine.Damage / 2f, mine.BlastRadius / 2f));
        }
    }
}
