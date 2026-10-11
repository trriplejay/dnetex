using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Every few seconds, creates a minion at the owner's position and throws it at a random spot near the player.
/// The minion flies there at speedMultiplier times its own speed, then behaves like a normal enemy of its type.
/// </summary>
/// <param name="minLandingDistance">Closest the landing spot can be to the player, so throws aren't instant hits.</param>
public sealed class ThrowMinions(
    EnemyType minion, float secondsBetweenThrows, float speedMultiplier, float minLandingDistance, float maxLandingDistance)
    : IEnemyAbility
{
    private float _sinceThrow;

    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
    {
        _sinceThrow += elapsedSeconds;
        var thrown = new List<Enemy>();
        while (_sinceThrow >= secondsBetweenThrows)
        {
            _sinceThrow -= secondsBetweenThrows;
            var angle = random.NextSingle() * MathF.Tau;
            var distance = minLandingDistance + random.NextSingle() * (maxLandingDistance - minLandingDistance);
            var spot = playerPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            var flight = new Thrown(spot, minion.Speed * speedMultiplier, minion.CreateMovement(minion.Speed));
            thrown.Add(new Enemy(minion, owner.Position, flight));
        }
        return thrown;
    }
}
