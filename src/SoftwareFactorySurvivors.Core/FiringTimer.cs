using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Where a shot leaves from and which way it flies.</summary>
public readonly record struct ShotPose(Vector2 Position, float Heading);

/// <summary>Emits shots at a rate given each frame, using the current pose supplied by the frame.</summary>
public sealed class FiringTimer
{
    /// <summary>Fraction of the way to the next shot; one shot per whole unit.</summary>
    private double _progress;

    /// <summary>Fraction of the way to the next shot, in [0, 1]; exactly 1 while a charged shot waits (see Charge).</summary>
    public double Progress => _progress;

    /// <summary>
    /// Runs the timer without firing: progress grows as in Update but stops at one whole shot, which then fires on
    /// the next Update. For frames with nothing to shoot at.
    /// </summary>
    public void Charge(float elapsedSeconds, float shotsPerSecond = 1f)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!float.IsFinite(shotsPerSecond) || shotsPerSecond <= 0f)
            throw new ArgumentOutOfRangeException(nameof(shotsPerSecond));

        _progress = Math.Min(1d, _progress + (double)elapsedSeconds * shotsPerSecond);
    }

    /// <param name="shotsPerSecond">Firing rate for this frame; a change carries over progress toward the next shot.</param>
    public IReadOnlyList<ShotPose> Update(float elapsedSeconds, Vector2 nose, float heading, float shotsPerSecond = 1f)
    {
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!float.IsFinite(shotsPerSecond) || shotsPerSecond <= 0f)
            throw new ArgumentOutOfRangeException(nameof(shotsPerSecond));

        _progress += (double)elapsedSeconds * shotsPerSecond;
        var shots = new List<ShotPose>();
        while (_progress >= 1d)
        {
            shots.Add(new ShotPose(nose, heading));
            _progress -= 1d;
        }
        return shots;
    }
}
