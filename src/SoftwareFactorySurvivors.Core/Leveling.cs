namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Experience and level. Each level needs 5 × level^1.5 experience (rounded): 5, 14, 26, 40, 56, 73, 93 …
/// The cost grows faster than linearly, so early levels come quickly to get the build going, while later
/// levels slow down; but it grows far slower than exponentially, so the tougher, higher-experience enemies
/// arriving later still keep upgrades coming at a steady pace all the way to 10:00.
/// </summary>
public sealed class Leveling
{
    public int Level { get; private set; } = 1;

    /// <summary>Experience earned toward the next level.</summary>
    public int Experience { get; private set; }

    public int ExperienceToNextLevel => Required(Level);

    public static int Required(int level) => (int)Math.Round(5 * Math.Pow(level, 1.5));

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
