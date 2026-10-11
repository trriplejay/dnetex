using System.Numerics;

namespace SoftwareFactorySurvivors.Core;

/// <summary>
/// Something an enemy does besides moving, such as a boss summoning minions. Each enemy owns its own instance,
/// so an ability may keep state (cooldowns).
/// </summary>
public interface IEnemyAbility
{
    /// <summary>Advances the ability and returns any new enemies it created this frame.</summary>
    IReadOnlyList<Enemy> Update(Enemy owner, Vector2 playerPosition, float elapsedSeconds, Random random);
}
