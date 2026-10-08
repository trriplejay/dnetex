using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Emits once per elapsed second, using the current pose supplied by the frame.</summary>
public sealed class FiringTimer
{
    private double _elapsedSeconds;

    public IReadOnlyList<Laser> Update(float elapsedSeconds, Vector2 nose, float heading)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        _elapsedSeconds += elapsedSeconds;
        var lasers = new List<Laser>();
        while (_elapsedSeconds >= 1d)
        {
            lasers.Add(new Laser(nose, heading));
            _elapsedSeconds -= 1d;
        }
        return lasers;
    }
}
