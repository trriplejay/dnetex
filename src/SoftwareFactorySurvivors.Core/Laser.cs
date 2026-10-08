using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

public sealed class Laser : Projectile
{
    /// <summary>Half the ship's 32-pixel nose-to-tail extent.</summary>
    public const float Length = 13f;

    public Laser(Vector2 position, float heading) : base(position, heading, 3 * Ship.Speed)
    {
    }

    public override Vector2[] GetLine() => [Position, Position + Forward * Length];
}
