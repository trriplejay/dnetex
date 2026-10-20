using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Something that hunts the player: it has hit points, a movement pattern and a drawn logo outline.</summary>
public sealed class Enemy
{
    /// <param name="movement">Overrides the type's own movement pattern, e.g. to hold an enemy still in tests.</param>
    /// <param name="experience">Overrides the experience the type is worth, e.g. 0 for minions a boss throws.</param>
    /// <param name="isCopy">True if this enemy is a look-alike copy (e.g. spawned by Hallucination split).</param>
    public Enemy(EnemyType type, Vector2 position, IMovementPattern? movement = null, int? experience = null, bool isCopy = false)
    {
        if (!(type.HitPoints > 0f))
            throw new ArgumentOutOfRangeException(nameof(type), "Enemy types need positive hit points.");

        if (type.Phases is { } phases && phases.Zip(phases.Skip(1)).Any(pair => pair.Second.AtHealthFraction >= pair.First.AtHealthFraction))
            throw new ArgumentException("Phases must be listed from the highest health fraction down.", nameof(type));

        Type = type;
        Position = position;
        HitPoints = type.HitPoints;
        Experience = experience ?? type.Experience;
        IsCopy = isCopy;
        Movement = movement ?? type.CreateMovement(type.Speed);
        CreateCurrentMovement = () => type.CreateMovement(type.Speed);
        Ability = isCopy ? null : type.CreateAbility?.Invoke();
        Shields = _newShields = CreateShields(type.Shield);
    }

    public EnemyType Type { get; }
    public Vector2 Position { get; private set; }
    public float HitPoints { get; private set; }

    /// <summary>True if this enemy is a copy/decoy that dies in one hit, gives 0 XP, and never flickers.</summary>
    public bool IsCopy { get; }

    /// <summary>Seconds of game time left on hit-flicker.</summary>
    public float FlickerSecondsLeft { get; private set; }

    /// <summary>True while this enemy is flickering after taking damage.</summary>
    public bool IsFlickering => FlickerSecondsLeft > 0f;

    /// <summary>Experience the player gains for killing this enemy.</summary>
    public int Experience { get; }
    public IMovementPattern Movement { get; private set; }

    /// <summary>
    /// Builds a fresh instance of the movement this enemy uses now: the type's CreateMovement at its Speed, until a
    /// reached phase sets its own CreateMovement. Lets an ability (e.g. SplitIntoCopies) give each copy its own
    /// pattern of the current kind, never sharing this enemy's instance.
    /// </summary>
    public Func<IMovementPattern> CreateCurrentMovement { get; private set; }

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

    /// <summary>
    /// Seconds of game time left on a Bola slow. It lives on the enemy rather than its movement pattern, so it
    /// carries over when a phase replaces the movement.
    /// </summary>
    public float SlowSecondsLeft { get; private set; }

    /// <summary>True while a Bola slow holds the enemy to a quarter of its normal movement.</summary>
    public bool IsSlowed => SlowSecondsLeft > 0f;

    public void Update(Vector2 playerPosition, float elapsedSeconds)
    {
        // A slowed enemy covers a quarter of its normal distance; its pattern's own timers and ramps still see the
        // real elapsed time, so it moves exactly as normal once the slow ends.
        Position = Movement.Move(Position, playerPosition, elapsedSeconds, IsSlowed ? 0.25f : 1f);
        Rotation += Type.SpinSpeed * elapsedSeconds;
        if (IsSlowed)
            SlowSecondsLeft = MathF.Max(0f, SlowSecondsLeft - elapsedSeconds);
        if (IsFlickering)
            FlickerSecondsLeft = MathF.Max(0f, FlickerSecondsLeft - elapsedSeconds);
    }

    /// <summary>Runs the enemy's special ability, if it has one, and returns any enemies it created.</summary>
    public IReadOnlyList<Enemy> UseAbility(Vector2 playerPosition, float elapsedSeconds, Random random) =>
        Ability?.Update(this, playerPosition, elapsedSeconds, random) ?? [];

    /// <param name="slow">
    /// True for a Bola hit: it also slows the enemy for 2 seconds, restarting (not stacking) any slow already on it.
    /// A hit the shield blocks neither damages nor slows.
    /// </param>
    public void TakeDamage(float damage, bool slow = false)
    {
        if (IsShielded)
            return;
        if (slow)
            SlowSecondsLeft = 2f;
        if (IsCopy)
        {
            HitPoints = 0f;
            return;
        }

        if (damage > 0f)
            FlickerSecondsLeft = 0.3f;

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
            {
                Movement = phase.CreateMovement();
                CreateCurrentMovement = phase.CreateMovement;
            }
            // If one hit crosses several phases, only the last phase's shield appears.
            if (phase.Shield is not null)
                Shields = _newShields = CreateShields(phase.Shield);
            if (phase.RetuneMovement is not null)
                phase.RetuneMovement(Movement);
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
