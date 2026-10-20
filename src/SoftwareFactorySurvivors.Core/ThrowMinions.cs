using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Every few seconds, creates minions at the owner's position and throws each at its own random spot near the
/// player. A throw can come in several waves a moment apart. Minions fly to their spots at speedMultiplier
/// times their own speed, then behave like normal enemies of their type, except that they're worth no experience
/// (so a boss can't be farmed for levels).
/// </summary>
/// <param name="minLandingDistance">Closest the landing spot can be to the player, so throws aren't instant hits.</param>
/// <param name="minionsPerWave">How many minions each wave throws at once.</param>
/// <param name="waves">How many waves each throw is made of.</param>
/// <param name="secondsBetweenWaves">Delay between the waves of one throw.</param>
public sealed class ThrowMinions(
    EnemyType minion, float secondsBetweenThrows, float speedMultiplier, float minLandingDistance, float maxLandingDistance,
    int minionsPerWave = 1, int waves = 1, float secondsBetweenWaves = 0f,
    float? firstThrowAfterSeconds = null, bool harmlessInFlight = false,
    Func<Enemy, bool>? gate = null)
    : IEnemyAbility
{
    private float _sinceThrow;
    private bool _hasThrownFirst;

    // Seconds until each scheduled wave goes; a wave is thrown once its countdown reaches zero.
    private readonly List<float> _wavesDueIn = [];

    public EnemyType Minion => minion;
    public float SecondsBetweenThrows => secondsBetweenThrows;
    public float SpeedMultiplier => speedMultiplier;
    public float MinLandingDistance => minLandingDistance;
    public float MaxLandingDistance => maxLandingDistance;
    public int MinionsPerWave => minionsPerWave;
    public int Waves => waves;
    public float SecondsBetweenWaves => secondsBetweenWaves;
    public float? FirstThrowAfterSeconds => firstThrowAfterSeconds;
    public bool HarmlessInFlight => harmlessInFlight;
    public Func<Enemy, bool>? Gate => gate;

    public IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random)
    {
        if (owner.IsDead)
            return [];

        if (gate is not null && !gate(owner))
            return [];

        for (var i = 0; i < _wavesDueIn.Count; i++)
            _wavesDueIn[i] -= elapsedSeconds;

        const float epsilon = 0.0005f;
        _sinceThrow += elapsedSeconds;
        while (true)
        {
            var interval = _hasThrownFirst ? secondsBetweenThrows : (firstThrowAfterSeconds ?? secondsBetweenThrows);
            if (_sinceThrow < interval - epsilon)
                break;

            _sinceThrow = _sinceThrow >= interval ? _sinceThrow - interval : 0f;
            _hasThrownFirst = true;
            // _sinceThrow is now how long ago this throw was due, so later waves keep their spacing from it.
            for (var wave = 0; wave < waves; wave++)
                _wavesDueIn.Add(wave * secondsBetweenWaves - _sinceThrow);
        }

        var thrown = new List<Enemy>();
        foreach (var _ in _wavesDueIn.Where(dueIn => dueIn <= 0f))
        {
            for (var i = 0; i < minionsPerWave; i++)
                thrown.Add(Throw(owner, playerPosition, random));
        }
        _wavesDueIn.RemoveAll(dueIn => dueIn <= 0f);
        return thrown;
    }

    private Enemy Throw(Enemy owner, Vector2 playerPosition, Random random)
    {
        var angle = random.NextSingle() * MathF.Tau;
        var distance = minLandingDistance + random.NextSingle() * (maxLandingDistance - minLandingDistance);
        var spot = playerPosition + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        var flight = new Thrown(spot, minion.Speed * speedMultiplier, minion.CreateMovement(minion.Speed), harmlessUntilLanded: harmlessInFlight);
        return new Enemy(minion, owner.Position, flight, experience: 0);
    }
}
