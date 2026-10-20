using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ClaudeBossNotchTests
{
    private static readonly Vector2 Center = new(500f, 400f);
    private static readonly Vector2 PlayerPosition = new(500f, 200f);

    [Fact]
    public void ClaudeBoss_phases_are_at_75_50_and_25_percent_with_bare_50_percent_phase()
    {
        var phases = EnemyRoster.ClaudeBoss.Phases;
        Assert.NotNull(phases);
        Assert.Equal(3, phases.Count);

        var fractions = phases.Select(p => p.AtHealthFraction).ToArray();
        Assert.Equal([0.75f, 0.50f, 0.25f], fractions);

        // 50% phase added for notched bar has no replacement ability, movement, or shield
        var phase50 = phases[1];
        Assert.Equal(0.50f, phase50.AtHealthFraction);
        Assert.Null(phase50.CreateAbility);
        Assert.Null(phase50.CreateMovement);
        Assert.Null(phase50.Shield);
    }

    [Fact]
    public void ClaudeBoss_single_hit_to_25_percent_advances_phases_without_resetting_ability_or_movement_or_spawning_extra_ring()
    {
        var boss = new Enemy(EnemyRoster.ClaudeBoss, Center);
        var rng = new Random(42);

        // Advance past first ring hazard (ClosingRing spawns immediately on step 1)
        boss.UseAbility(PlayerPosition, 0.1f, rng);
        var initialHazards = ((IHazardProducer)boss.Ability!).TakeHazards();
        Assert.Contains(initialHazards, h => h.Kind == HazardKind.Ring);

        var capturedAbility = boss.Ability;
        var capturedMovement = boss.Movement;
        Assert.NotNull(capturedAbility);
        Assert.NotNull(capturedMovement);

        // In one TakeDamage call, take boss down from 120 HP to 30 HP (<= 25% of 120, but alive)
        // Damage = 90f (120 - 90 = 30)
        boss.TakeDamage(90f);

        Assert.False(boss.IsDead);
        Assert.Equal(30f, boss.HitPoints);
        Assert.Equal(3, boss.PhasesReached);

        // Ability and Movement instances are preserved across all 3 phases
        Assert.Same(capturedAbility, boss.Ability);
        Assert.Same(capturedMovement, boss.Movement);

        // Subsequent updates before 10s elapsed since last ring yield no HazardKind.Ring
        // We advance 5 seconds in 0.1s steps (total time ~5.1s < 10.0s cooldown)
        for (var i = 0; i < 50; i++)
        {
            boss.UseAbility(PlayerPosition, 0.1f, rng);
            var hazards = ((IHazardProducer)boss.Ability).TakeHazards();
            Assert.DoesNotContain(hazards, h => h.Kind == HazardKind.Ring);
        }
    }
}
