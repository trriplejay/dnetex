using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Something that hunts the player: it has hit points, a movement pattern and a drawn logo outline.</summary>
public sealed class Enemy
{
    /// <param name="movement">Overrides the type's own movement pattern, e.g. to hold an enemy still in tests.</param>
    public Enemy(EnemyType type, Vector2 position, IMovementPattern? movement = null)
    {
        if (!(type.HitPoints > 0f))
            throw new ArgumentOutOfRangeException(nameof(type), "Enemy types need positive hit points.");

        Type = type;
        Position = position;
        HitPoints = type.HitPoints;
        Movement = movement ?? type.CreateMovement(type.Speed);
        Ability = type.CreateAbility?.Invoke();
    }

    public EnemyType Type { get; }
    public Vector2 Position { get; private set; }
    public float HitPoints { get; private set; }
    public IMovementPattern Movement { get; }
    public IEnemyAbility? Ability { get; }

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
        HitPoints = MathF.Max(0f, HitPoints - damage);
    }

    /// <summary>The logo outline in screen coordinates, used for both drawing and contact.</summary>
    public IReadOnlyList<Vector2[]> GetStrokes()
    {
        var transform = Matrix3x2.CreateRotation(Rotation) * Matrix3x2.CreateTranslation(Position);
        return Type.Outline.Select(stroke => stroke.Select(p => Vector2.Transform(p, transform)).ToArray()).ToArray();
    }
}
