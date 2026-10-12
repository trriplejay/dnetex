using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>Helpers for tests that need predictable enemies and sessions.</summary>
internal static class TestEnemies
{
    /// <summary>A Gemini that never moves, for tests that need a target to stay put.</summary>
    public static Enemy StationaryGemini(Vector2 position) => Stationary(EnemyRoster.Gemini, position);

    public static Enemy Stationary(EnemyType type, Vector2 position) => new(type, position, new Stay());

    /// <summary>A session with no enemy spawns, bosses or rings, so only what a test adds is in play.</summary>
    public static GameSession QuietSession(Vector2 shipPosition) =>
        new(shipPosition, new Random(1), roster: [], bosses: [], rings: []);

    private sealed class Stay : IMovementPattern
    {
        public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds) => position;
    }
}
