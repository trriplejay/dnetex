using System.Globalization;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Where a new game starts, for debugging: the clock time and the ship's level. Starting above level 1
/// offers the upgrade cards for every level skipped before play begins.
/// </summary>
public sealed record StartOptions(float StartSeconds = 0f, int StartLevel = 1)
{
    public const string Usage = "Usage: dotnet run --project src/SoftwareFactorySurvivors.Desktop -- [--time MM:SS] [--level N]";

    public static StartOptions Default { get; } = new();

    /// <summary>Reads <c>--time MM:SS</c> (or whole seconds) and <c>--level N</c>; either may also be written as --name=value.</summary>
    /// <exception cref="ArgumentException">An option is unknown, missing its value or out of range.</exception>
    public static StartOptions Parse(IReadOnlyList<string> args)
    {
        var options = Default;
        for (var i = 0; i < args.Count; i++)
        {
            var (name, value) = SplitOption(args, ref i);
            options = name switch
            {
                "--time" => options with { StartSeconds = ParseTime(value) },
                "--level" => options with { StartLevel = ParseLevel(value) },
                _ => throw new ArgumentException($"Unknown option '{name}'. {Usage}"),
            };
        }
        return options;
    }

    private static (string Name, string Value) SplitOption(IReadOnlyList<string> args, ref int i)
    {
        var arg = args[i];
        var equals = arg.IndexOf('=');
        if (equals >= 0)
            return (arg[..equals], arg[(equals + 1)..]);
        if (i + 1 >= args.Count)
            throw new ArgumentException($"Option '{arg}' needs a value. {Usage}");
        return (arg, args[++i]);
    }

    private static float ParseTime(string value)
    {
        var parts = value.Split(':');
        var seconds = parts.Length switch
        {
            1 when int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var s) => s,
            2 when int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var m)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var s) && s < 60 => m * 60 + s,
            _ => throw new ArgumentException($"Time '{value}' should look like 2:30 or 150. {Usage}"),
        };
        if (seconds >= GameSession.SurvivalSeconds)
            throw new ArgumentException($"Time '{value}' must be before {GameClock.Format(GameSession.SurvivalSeconds)}.");
        return seconds;
    }

    private static int ParseLevel(string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level < 1)
            throw new ArgumentException($"Level '{value}' should be a whole number of at least 1. {Usage}");
        return level;
    }
}
