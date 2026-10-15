using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Something that hunts the player: it has hit points, a movement pattern and a drawn logo outline.</summary>
public sealed class Enemy
{
    /// <param name="movement">Overrides the type's own movement pattern, e.g. to hold an enemy still in tests.</param>
    /// <param name="experience">Overrides the experience the type is worth, e.g. 0 for minions a boss throws.</param>
    public Enemy(EnemyType type, Vector2 position, IMovementPattern? movement = null, int? experience = null)
    {
        if (!(type.HitPoints > 0f))
            throw new ArgumentOutOfRangeException(nameof(type), "Enemy types need positive hit points.");

        if (type.Phases is { } phases && phases.Zip(phases.Skip(1)).Any(pair => pair.Second.AtHealthFraction >= pair.First.AtHealthFraction))
            throw new ArgumentException("Phases must be listed from the highest health fraction down.", nameof(type));

        Type = type;
        Position = position;
        HitPoints = type.HitPoints;
        Experience = experience ?? type.Experience;
        Movement = movement ?? type.CreateMovement(type.Speed);
        Ability = type.CreateAbility?.Invoke();
        Shields = _newShields = CreateShields(type.Shield);
    }

    public EnemyType Type { get; }
    public Vector2 Position { get; private set; }
    public float HitPoints { get; private set; }

    /// <summary>Experience the player gains for killing this enemy.</summary>
    public int Experience { get; }
    public IMovementPattern Movement { get; private set; }
    public IEnemyAbility? Ability { get; private set; }

    /// <summary>The enemies orbiting this one as its current shield (a phase can replace it with a fresh one).</summary>
    public IReadOnlyList<Enemy> Shields { get; private set; }

    // Shield enemies created since the game last collected them with TakeNewShields.
    private IReadOnlyList<Enemy> _newShields;

    /// <summary>Returns shield enemies spawned since the last call, so the game can add them to play.</summary>
    public IReadOnlyList<Enemy> TakeNewShields()
    {
        var created = _newShields;
        _newShields = [];
        return created;
    }

    /// <summary>True while any shield enemy is alive; damage is ignored until they're all dead.</summary>
    public bool IsShielded => Shields.Any(shield => !shield.IsDead);

    /// <summary>How many of the type's Phases have kicked in so far (0 before the first).</summary>
    public int PhasesReached { get; private set; }

    /// <summary>Radians the logo has spun since it spawned.</summary>
    public float Rotation { get; private set; }

    public bool IsDead => HitPoints <= 0f;

    public void Update(Vector2 playerPosition, float elapsedSeconds)
    {
        Position = Movement.Move(Position, playerPosition, elapsedSeconds);
        Rotation += Type.SpinSpeed * elapsedSeconds;
    }

    /// <summary>Runs the enemy's special ability, if it has one, and returns any enemies it created.</summary>
    public IReadOnlyList<Enemy> UseAbility(Vector2 playerPosition, float elapsedSeconds, Random random) =>
        Ability?.Update(this, playerPosition, elapsedSeconds, random) ?? [];

    public void TakeDamage(float damage)
    {
        if (IsShielded)
            return;
        HitPoints = MathF.Max(0f, HitPoints - damage);
        EnterReachedPhases();
    }

    private Enemy[] CreateShields(EnemyShield? shield)
    {
        if (shield is null)
            return [];

        return Enumerable.Range(0, shield.Count).Select(i =>
        {
            var orbit = new Orbit(this, shield.OrbitRadius, shield.Type.Speed * shield.SpeedMultiplier, MathF.Tau * i / shield.Count);
            return new Enemy(shield.Type, orbit.CurrentPosition, orbit);
        }).ToArray();
    }

    // A single big hit can cross several thresholds; each one's changes apply in order.
    private void EnterReachedPhases()
    {
        var phases = Type.Phases ?? [];
        while (!IsDead && PhasesReached < phases.Count && HitPoints <= Type.HitPoints * phases[PhasesReached].AtHealthFraction)
        {
            var phase = phases[PhasesReached++];
            if (phase.CreateAbility is not null)
                Ability = phase.CreateAbility();
            if (phase.CreateMovement is not null)
                Movement = phase.CreateMovement();
            // If one hit crosses several phases, only the last phase's shield appears.
            if (phase.Shield is not null)
                Shields = _newShields = CreateShields(phase.Shield);
        }
    }

    // The outline last returned by GetStrokes and the position, rotation and local outline it was built from.
    private Vector2[][]? _strokes;
    private Vector2 _strokesPosition;
    private float _strokesRotation;
    private IReadOnlyList<Vector2[]>? _strokesOutline;

    /// <summary>
    /// The logo outline in screen coordinates, used for both drawing and contact. Several callers read it each
    /// frame, so it is rebuilt only when the position, rotation or outline has changed since the last call; otherwise
    /// the same instance comes back without allocating. A rebuild makes new arrays, so an outline a caller already
    /// holds never changes under it. Callers must not modify the returned arrays.
    /// </summary>
    public IReadOnlyList<Vector2[]> GetStrokes()
    {
        var outline = Type.Outline;
        if (_strokes is not null && _strokesPosition == Position && _strokesRotation.Equals(Rotation) && ReferenceEquals(_strokesOutline, outline))
            return _strokes;

        var transform = Matrix3x2.CreateRotation(Rotation) * Matrix3x2.CreateTranslation(Position);
        var strokes = new Vector2[outline.Count][];
        for (var i = 0; i < strokes.Length; i++)
        {
            var local = outline[i];
            var stroke = new Vector2[local.Length];
            for (var j = 0; j < stroke.Length; j++)
                stroke[j] = Vector2.Transform(local[j], transform);
            strokes[i] = stroke;
        }

        (_strokes, _strokesPosition, _strokesRotation, _strokesOutline) = (strokes, Position, Rotation, outline);
        return strokes;
    }
}
