using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Emits shots at a rate given each frame, using the current pose supplied by the frame.</summary>
public sealed class FiringTimer
{
    /// <summary>Fraction of the way to the next shot; one shot per whole unit.</summary>
    private double _progress;

    /// <param name="shotsPerSecond">Firing rate for this frame; a change carries over progress toward the next shot.</param>
    public IReadOnlyList<Laser> Update(float elapsedSeconds, Vector2 nose, float heading, float shotsPerSecond = 1f)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!float.IsFinite(shotsPerSecond) || shotsPerSecond <= 0f)
            throw new ArgumentOutOfRangeException(nameof(shotsPerSecond));

        _progress += (double)elapsedSeconds * shotsPerSecond;
        var lasers = new List<Laser>();
        while (_progress >= 1d)
        {
            lasers.Add(new Laser(nose, heading));
            _progress -= 1d;
        }
        return lasers;
    }
}
