namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// A ship enhancement that upgrade cards can offer: picked once into one of the ship's enhancement slots, then
/// raised a level per card up to OwnedEnhancement.MaxLevel. Its effects belong to whoever defines it.
/// </summary>
/// <param name="Name">Card title, e.g. "SHIELD".</param>
/// <param name="Effect">Short card line describing what it does.</param>
public sealed record Enhancement(string Name, string Effect);

/// <summary>An enhancement the ship owns and its level.</summary>
public sealed class OwnedEnhancement
{
    public const int MaxLevel = 20;

    public OwnedEnhancement(Enhancement definition) => Definition = definition;

    public Enhancement Definition { get; }

    /// <summary>1 when picked; each upgrade card raises it by one, up to MaxLevel.</summary>
    public int Level { get; private set; } = 1;

    /// <summary>Raises the level by one; does nothing once the enhancement is at MaxLevel.</summary>
    public void RaiseLevel() => Level = Math.Min(MaxLevel, Level + 1);
}
