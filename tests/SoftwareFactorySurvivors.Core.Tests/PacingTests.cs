using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>How level-up rounds are paced: their order, short offers, and levels with nothing left to offer.</summary>
public class PacingTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);
    private const float Frame = 1f / 60f;

    [Fact]
    public void Fewer_than_three_eligible_cards_offers_only_those()
    {
        // Every weapon has at least 3 options, so only a lone enhancement below level 5 leaves fewer than 3 cards.
        var bravo = new Enhancement("BRAVO", "B");
        var stats = MaxedWeapons();
        stats.AddEnhancement(bravo);
        for (var i = 0; i < 3; i++)
            Upgrades.Apply(Upgrade.ForEnhancement(bravo), stats);
        Assert.Equal(4, stats.Find(bravo)!.Level);

        var offer = Upgrades.Offer(stats, new Random(1), GameSession.UpgradeChoices, [bravo]);

        Assert.Equal([Upgrade.ForEnhancement(bravo)], offer);
    }

    [Fact]
    public void Levels_with_no_eligible_card_are_used_up_without_pausing_and_the_queue_keeps_draining()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.AddWeapon(WeaponKind.Lightning);
        session.Stats.AddWeapon(WeaponKind.Orbs);
        foreach (var weapon in session.Stats.Weapons)
        {
            while (weapon.Level < OwnedWeapon.MaxLevel)
                Upgrades.Apply(Upgrade.ForWeapon(weapon.Kind, Upgrades.OptionsFor(weapon.Kind)[0]), session.Stats);
        }
        Assert.Empty(Upgrades.Eligible(session.Stats));
        session.Stats.Laser().Damage = 1000f;

        // One kill worth exactly two levels.
        var prize = EnemyRoster.Meta with { Experience = Leveling.Required(1) + Leveling.Required(2) };
        session.AddEnemy(TestEnemies.Stationary(prize, session.Ship.Nose - new Vector2(0, 150)));
        for (var i = 0; i < 60 * 3 && session.Leveling.Level < 3; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Equal(3, session.Leveling.Level);
        Assert.False(session.IsChoosingUpgrade);
        Assert.Null(session.UpgradeOffer);
        var clock = session.ElapsedSeconds;
        session.Update(Vector2.Zero, null, 0.5f, Width, Height);
        Assert.Equal(clock + 0.5f, session.ElapsedSeconds, precision: 4);
        Assert.Throws<InvalidOperationException>(() => session.ChooseUpgrade(0));
    }

    [Fact]
    public void An_out_of_range_choice_is_rejected_and_leaves_the_offer_in_place()
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], rings: []);
        var offer = session.UpgradeOffer!;

        Assert.Throws<ArgumentOutOfRangeException>(() => session.ChooseUpgrade(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.ChooseUpgrade(offer.Count));

        Assert.Same(offer, session.UpgradeOffer);
        Assert.True(session.IsStartingPick);
        Assert.Empty(session.Stats.Weapons);
    }

    [Fact]
    public void Starting_above_level_1_offers_the_pick_first_then_one_regular_round_per_skipped_level()
    {
        var session = new GameSession(Center, new Random(5), roster: [], bosses: [], rings: [], start: new StartOptions(StartLevel: 3));

        Assert.True(session.IsStartingPick);
        Assert.Equal(Upgrades.StartingWeapons, session.UpgradeOffer!);
        session.ChooseUpgrade(0);

        for (var round = 0; round < 2; round++)
        {
            Assert.True(session.IsChoosingUpgrade, $"Round {round + 1} wasn't offered");
            Assert.False(session.IsStartingPick);
            var eligible = Upgrades.Eligible(session.Stats);
            Assert.All(session.UpgradeOffer!, card => Assert.Contains(card, eligible));
            session.Update(Vector2.Zero, null, 1f, Width, Height);
            Assert.Equal(0f, session.ElapsedSeconds);
            session.ChooseUpgrade(0);
        }

        Assert.False(session.IsChoosingUpgrade);
        // The pick gave one weapon level and each of the two rounds exactly one more.
        Assert.Equal(3, session.Stats.Weapons.Sum(weapon => weapon.Level));
        session.Update(Vector2.Zero, null, 1f, Width, Height);
        Assert.Equal(1f, session.ElapsedSeconds);
    }

    /// <summary>Stats owning all three weapons, each at level 5.</summary>
    private static PlayerStats MaxedWeapons()
    {
        var stats = new PlayerStats();
        foreach (var kind in Upgrades.AllWeapons)
        {
            var weapon = stats.AddWeapon(kind);
            while (weapon.Level < OwnedWeapon.MaxLevel)
                Upgrades.Apply(Upgrade.ForWeapon(kind, Upgrades.OptionsFor(kind)[0]), stats);
        }
        return stats;
    }
}
