namespace SoftwareFactorySurvivors.Core;

/// <summary>Formats survival time for the on-screen clock.</summary>
public static class GameClock
{
    /// <summary>Whole minutes and seconds as MM:SS, e.g. 83.9 seconds is "01:23".</summary>
    public static string Format(float seconds)
    {
        var whole = (int)MathF.Floor(MathF.Max(seconds, 0f));
        return $"{whole / 60:00}:{whole % 60:00}";
    }
}
