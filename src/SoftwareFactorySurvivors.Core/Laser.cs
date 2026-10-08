using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

public sealed class Laser : Projectile
{
    /// <summary>The laser's fixed 13-pixel beam. The ship's nose-to-tail extent is 32 pixels (16 forward + 16 back).</summary>
    public const float Length = 13f;

    public Laser(Vector2 position, float heading) : base(position, heading, 3 * Ship.Speed)
    {
    }

    public override Vector2[] GetLine() => [Position, Position + Forward * Length];
}
