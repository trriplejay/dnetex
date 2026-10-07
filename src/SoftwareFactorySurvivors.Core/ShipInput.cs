namespace SoftwareFactorySurvivors.Core;

/// <summary>Translates held controls into independent thrust and rotation intents.</summary>
public static class ShipInput
{
    /// <summary>Opposing controls cancel on each axis; released controls contribute zero.</summary>
    public static (float Thrust, float Rotation) FromKeys(bool forward, bool backward, bool left, bool right)
        => ((forward ? 1f : 0f) - (backward ? 1f : 0f),
            (right ? 1f : 0f) - (left ? 1f : 0f));
}
