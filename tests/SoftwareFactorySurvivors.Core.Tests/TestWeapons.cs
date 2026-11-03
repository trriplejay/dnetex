using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>Helpers for tests that tune an owned weapon's own stats.</summary>
internal static class TestWeapons
{
    /// <summary>The ship's laser; fails the test when the ship doesn't own one.</summary>
    public static OwnedWeapon Laser(this PlayerStats stats) =>
        stats.Find(WeaponKind.Laser) ?? throw new InvalidOperationException("The ship owns no laser.");
}
