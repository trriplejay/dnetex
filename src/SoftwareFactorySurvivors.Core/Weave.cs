using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>Closes in on the player while swaying side to side like a snake.</summary>
/// <param name="amplitude">How far, in pixels, the sway carries the enemy to either side.</param>
/// <param name="cyclesPerSecond">How many full side-to-side sways per second.</param>
public sealed class Weave(float speed, float amplitude, float cyclesPerSecond) : IMovementPattern
{
    private float _time;

    public Vector2 Move(Vector2 position, Vector2 playerPosition, float elapsedSeconds)
    {
        _time += elapsedSeconds;
        var toPlayer = playerPosition - position;
        var distance = toPlayer.Length();
        if (distance < 0.001f)
            return position;

        var forward = toPlayer / distance;
        var side = new Vector2(-forward.Y, forward.X);
        var angularFrequency = MathF.Tau * cyclesPerSecond;
        var sway = side * amplitude * angularFrequency * MathF.Cos(angularFrequency * _time);
        var step = MathF.Min(speed * elapsedSeconds, distance);
        return position + forward * step + sway * elapsedSeconds;
    }
}
