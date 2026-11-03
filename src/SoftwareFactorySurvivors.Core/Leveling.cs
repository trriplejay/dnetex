namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Experience and level. Each level needs max(1, round(2.0 × level^1.25)) experience: 2, 5, 8, 11, 15, 19, 23, 27, 31, 36 …
/// The cost grows faster than linearly, so early levels come quickly to get the build going, while later
/// levels slow down; but it grows far slower than exponentially, so the tougher, higher-experience enemies
/// arriving later still keep upgrades coming at a steady pace all the way to 10:00.
/// </summary>
public sealed class Leveling
{
    public Leveling(int startLevel = 1)
    {
        if (startLevel < 1)
            throw new ArgumentOutOfRangeException(nameof(startLevel));
        Level = startLevel;
    }

    /// <summary>Scales the experience every level needs; lower means faster leveling.</summary>
    public const double ExperienceScale = 2.0;

    public int Level { get; private set; }

    /// <summary>Experience earned toward the next level.</summary>
    public int Experience { get; private set; }

    public int ExperienceToNextLevel => Required(Level);

    public static int Required(int level) => Math.Max(1, (int)Math.Round(ExperienceScale * Math.Pow(level, 1.25)));

    /// <summary>Adds experience and returns how many levels were gained (can be more than one at once).</summary>
    public int Add(int experience)
    {
        if (experience < 0)
            throw new ArgumentOutOfRangeException(nameof(experience));

        Experience += experience;
        var gained = 0;
        while (Experience >= ExperienceToNextLevel)
        {
            Experience -= ExperienceToNextLevel;
            Level++;
            gained++;
        }
        return gained;
    }
}
