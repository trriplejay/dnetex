using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>Static check: the retired weapon paths, the Bola slow and the old cards are gone from Core's source.</summary>
public class RetiredWeaponsSourceTests
{
    private static readonly Regex Retired =
        new(@"\b(WeaponPath|WeaponPathLevel|Ballista|Bola|SlowSecondsLeft|IsSlowed|ProjectileSize|Piercing)\b", RegexOptions.Compiled);

    [Fact]
    public void Core_source_names_no_retired_weapon_path_slow_or_card()
    {
        var core = Path.Combine(RepoRoot(), "src", "SoftwareFactorySurvivors.Core");
        var files = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .ToArray();
        Assert.NotEmpty(files);

        var hits = files
            .SelectMany(file => File.ReadLines(file).Select((line, i) => (file, line, number: i + 1)))
            .Where(entry => Retired.IsMatch(entry.line))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.number}: {entry.line.Trim()}")
            .ToArray();

        Assert.True(hits.Length == 0, "Retired names still in Core:\n" + string.Join("\n", hits));
        Assert.False(File.Exists(Path.Combine(core, "Ballista.cs")));
        Assert.False(File.Exists(Path.Combine(core, "Bola.cs")));
    }

    // The directory of this source file, two levels below the repository root.
    private static string RepoRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
}
