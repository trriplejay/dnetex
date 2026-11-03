using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>
/// Static checks on Core's source and the README: every loss of ship hit points goes through one method, and the
/// docs describe the 100 HP, ticking contact-damage rule with none of the old one-hit wording.
/// </summary>
public class ShipDamageSourceTests
{
    // An assignment that can lower the ship's hit points: "Stats.HitPoints =" or "Stats.HitPoints -=" (not "+=" or "==").
    private static readonly Regex LowersShipHitPoints = new(@"\bStats\.HitPoints\s*(-=|=(?!=))", RegexOptions.Compiled);

    [Fact]
    public void Exactly_one_site_in_Core_lowers_the_ships_hit_points_and_it_is_the_damage_to_ship_method()
    {
        var hits = CoreLines()
            .Where(entry => LowersShipHitPoints.IsMatch(entry.Line))
            .ToArray();

        var site = Assert.Single(hits);
        Assert.Equal("GameSession.cs", site.File);

        // The site sits inside DamageShip, and ResolveContact hands every source to it.
        var session = File.ReadAllText(Path.Combine(CoreDirectory(), "GameSession.cs"));
        var damageShip = session.IndexOf("private void DamageShip(float damage)", StringComparison.Ordinal);
        Assert.True(damageShip >= 0, "GameSession has no DamageShip method");
        var nextMember = session.IndexOf("\n    }", damageShip, StringComparison.Ordinal);
        Assert.InRange(session.IndexOf(site.Line.Trim(), damageShip, StringComparison.Ordinal), damageShip, nextMember);

        var resolveStart = session.IndexOf("private void ResolveContact(", StringComparison.Ordinal);
        var resolveEnd = session.IndexOf("private int ContactTicksDue(", StringComparison.Ordinal);
        var resolve = session[resolveStart..resolveEnd];
        // Enemies, bosses and ticking hazards (trail, ring) share ticks; bullets hit once: three calls in all.
        Assert.Equal(3, Regex.Matches(resolve, @"\bDamageShip\(").Count);
    }

    [Fact]
    public void Class_doc_and_README_describe_100_hp_and_ticking_contact_damage_with_no_one_hit_wording()
    {
        var session = File.ReadAllText(Path.Combine(CoreDirectory(), "GameSession.cs"));
        var classDoc = session[..session.IndexOf("public sealed class GameSession", StringComparison.Ordinal)];
        var readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));

        Assert.Contains("100 hit points", classDoc);
        Assert.Contains("ContactTickSeconds", classDoc);
        Assert.Contains("survive the contact", classDoc);
        Assert.Contains("100 hit points", readme);
        Assert.Contains("every 0.5 s while it stays in contact", readme);
        Assert.Contains("Enemies survive the contact", readme);

        var retired = new Regex(@"1 hit point|first touch is fatal|destroys the enemy|ignore contact|immun|InvulnerableSeconds\b",
            RegexOptions.IgnoreCase);
        Assert.DoesNotMatch(retired, classDoc);
        Assert.DoesNotMatch(retired, readme);
        Assert.DoesNotContain("const float InvulnerableSeconds", session);
    }

    private static IEnumerable<(string File, string Line)> CoreLines() =>
        Directory.EnumerateFiles(CoreDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .SelectMany(file => File.ReadLines(file).Select(line => (Path.GetFileName(file), line)));

    private static string CoreDirectory() => Path.Combine(RepoRoot(), "src", "SoftwareFactorySurvivors.Core");

    // The directory of this source file, two levels below the repository root.
    private static string RepoRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
}
