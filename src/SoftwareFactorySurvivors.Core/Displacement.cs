using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

internal static class Displacement
{
    /// <summary>
    /// Covers speedFactor of the way from position to the unslowed next position. The unslowed move is worked out
    /// first, snaps and clamps included, so a slowed enemy moves exactly speedFactor times as far as it would have.
    /// </summary>
    public static Vector2 Scale(Vector2 position, Vector2 unslowedNext, float speedFactor) =>
        speedFactor == 1f ? unslowedNext : position + (unslowedNext - position) * speedFactor;
}
