using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class AutocompleteNotchTests
{
    [Fact]
    public void Autocomplete_phases_fractions_are_exactly_75_50_and_25_in_descending_order()
    {
        // CLM-4: EnemyRoster.Autocomplete.Phases is non-null and lists exactly the AtHealthFraction
        // values [0.75, 0.50, 0.25], highest first.
        var phases = EnemyRoster.Autocomplete.Phases;
        Assert.NotNull(phases);

        var fractions = phases.Select(p => p.AtHealthFraction).ToArray();
        Assert.Equal([0.75f, 0.50f, 0.25f], fractions);
    }

    [Fact]
    public void Autocomplete_bare_phases_entries_change_no_behaviour_staged_and_single_hit()
    {
        // CLM-1: Crossing a bare Autocomplete Phases entry added only for its notch
        // (no CreateAbility, CreateMovement, Shield or RetuneMovement) changes no Autocomplete behaviour.
        // Movement stays the same ChargeAlongLine instance (ReferenceEquals) with unchanged ChargeSpeed,
        // ChargeDistance and TelegraphSeconds. Ability stays as it was. PhasesReached increments.
        // Bare entries are decided from the live roster, never by a hardcoded index.
        var phases = EnemyRoster.Autocomplete.Phases;
        Assert.NotNull(phases);

        // Identify bare entries from the live roster
        var barePhases = phases
            .Where(p => p.CreateAbility is null
                     && p.CreateMovement is null
                     && p.Shield is null
                     && p.RetuneMovement is null)
            .ToList();

        // Autocomplete's 50% and 25% entries now carry behaviour (bullet rings, charge retune),
        // so none is bare; whatever bare entries remain are held to the no-change rule below.
        // 1. Staged crossings across all bare phases
        TestStagedCrossing(barePhases);

        // 2. Single TakeDamage crossing all phases at once
        TestSingleHitCrossing();
    }

    private static void TestStagedCrossing(List<BossPhase> barePhases)
    {
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 200f));
        var initialMovement = boss.Movement;
        var initialAbility = boss.Ability;
        var chargeAlongLine = Assert.IsType<ChargeAlongLine>(boss.Movement);

        var initialSpeed = chargeAlongLine.ChargeSpeed;
        var initialDistance = chargeAlongLine.ChargeDistance;
        var initialTelegraph = chargeAlongLine.TelegraphSeconds;

        var phases = EnemyRoster.Autocomplete.Phases!;
        for (var i = 0; i < phases.Count; i++)
        {
            var phase = phases[i];
            var isBare = barePhases.Contains(phase);

            var movementBefore = boss.Movement;
            var abilityBefore = boss.Ability;
            var chargeBefore = Assert.IsType<ChargeAlongLine>(boss.Movement);
            var speedBefore = chargeBefore.ChargeSpeed;
            var distBefore = chargeBefore.ChargeDistance;
            var telegraphBefore = chargeBefore.TelegraphSeconds;
            var expectedPhasesReached = i + 1;

            // Damage just enough to cross this phase threshold (or slightly below)
            var targetHp = EnemyRoster.Autocomplete.HitPoints * phase.AtHealthFraction;
            var damageNeeded = MathF.Max(0f, boss.HitPoints - targetHp);
            if (damageNeeded > 0f)
            {
                boss.TakeDamage(damageNeeded);
            }

            Assert.Equal(expectedPhasesReached, boss.PhasesReached);

            // No Autocomplete phase swaps its movement instance.
            Assert.Same(movementBefore, boss.Movement);

            if (isBare)
            {
                Assert.Same(abilityBefore, boss.Ability);
                Assert.Equal(speedBefore, chargeBefore.ChargeSpeed);
                Assert.Equal(distBefore, chargeBefore.ChargeDistance);
                Assert.Equal(telegraphBefore, chargeBefore.TelegraphSeconds);
            }
        }
    }

    private static void TestSingleHitCrossing()
    {
        var boss = new Enemy(EnemyRoster.Autocomplete, new Vector2(100f, 200f));
        var initialMovement = boss.Movement;
        Assert.Null(boss.Ability);
        var charge = Assert.IsType<ChargeAlongLine>(boss.Movement);
        var initialTelegraph = charge.TelegraphSeconds;

        // Damage down to 10% in a single TakeDamage hit (Autocomplete has 60 HP; 6 HP = 10%)
        boss.TakeDamage(54f);

        Assert.Equal(6f, boss.HitPoints);
        Assert.False(boss.IsDead);
        Assert.Equal(3, boss.PhasesReached);

        // Movement instance remains ReferenceEquals
        Assert.Same(initialMovement, boss.Movement);
        Assert.IsType<BulletBursts>(boss.Ability);
        Assert.Equal(initialTelegraph, charge.TelegraphSeconds);
    }
}
