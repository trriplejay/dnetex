using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ShipEnhancementTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);

    // ---- Contact damage growing with time ----

    [Fact]
    public void Contact_damage_scale_is_1_at_the_start_2_at_ten_minutes_3_at_twenty_and_keeps_rising()
    {
        Assert.Equal(1f, EnemyRoster.ContactDamageScale(0f));
        Assert.Equal(2f, EnemyRoster.ContactDamageScale(600f));
        Assert.Equal(3f, EnemyRoster.ContactDamageScale(1200f));

        var previous = EnemyRoster.ContactDamageScale(0f);
        for (var seconds = 15f; seconds <= 1800f; seconds += 15f)
        {
            var scale = EnemyRoster.ContactDamageScale(seconds);
            Assert.True(scale > previous, $"Scale at {seconds} s ({scale}) is not above {previous}");
            previous = scale;
        }

        // Damage is base x scale, rounded to hundredths: 7 at 1:17 is 7.898... -> 7.90.
        Assert.Equal(6f, EnemyRoster.ScaledContactDamage(6f, 0f));
        Assert.Equal(30f, EnemyRoster.ScaledContactDamage(15f, 600f));
        Assert.Equal(7.9f, EnemyRoster.ScaledContactDamage(7f, 77f));
    }

    [Fact]
    public void Regular_enemies_deal_5_to_8_base_contact_damage_and_bosses_15()
    {
        Assert.All(EnemyRoster.All, type => Assert.InRange(type.ContactDamage, 5f, 8f));
        Assert.All(EnemyRoster.Bosses, arrival => Assert.Equal(15f, arrival.Type.ContactDamage));
    }

    [Fact]
    public void A_touch_at_ten_minutes_costs_twice_a_touch_at_the_start()
    {
        var atStart = FirstTouchLoss(0f);
        var atTen = FirstTouchLoss(600f);

        Assert.Equal(EnemyRoster.Grok.ContactDamage, atStart);
        Assert.Equal(2f * atStart, atTen);
    }

    private static float FirstTouchLoss(float startSeconds)
    {
        var session = new GameSession(Center, new Random(1), roster: [], bosses: [], rings: [],
            start: new StartOptions(StartSeconds: startSeconds)).WithLaser();
        session.AddEnemy(TestEnemies.Stationary(EnemyRoster.Grok with { HitPoints = 1_000f }, Center));

        // A frame too short to move the clock's scale off its hundredths.
        session.Update(Vector2.Zero, null, 0.0001f, Width, Height);

        return 100f - session.Stats.HitPoints;
    }

    // ---- Shield: capacity, absorbing, recharge (CLM-7) ----

    private const float Frame = 1f / 60f;

    private static GameSession QuietSessionWith(params (Enhancement Enhancement, int Level)[] enhancements)
    {
        var session = TestEnemies.QuietSession(Center);
        foreach (var (enhancement, level) in enhancements)
        {
            Upgrades.Apply(Upgrade.NewEnhancement(enhancement), session.Stats);
            for (var l = 1; l < level; l++)
                Upgrades.Apply(Upgrade.ForEnhancement(enhancement), session.Stats);
        }
        return session;
    }

    private static void Step(GameSession session, float seconds = Frame) =>
        session.Update(Vector2.Zero, null, seconds, Width, Height);

    [Theory]
    [InlineData(1, 5f, 10f)]
    [InlineData(2, 10f, 9f)]
    [InlineData(3, 15f, 8f)]
    [InlineData(4, 20f, 7f)]
    [InlineData(5, 25f, 5f)]
    public void A_shield_starts_full_at_5_per_level_and_waits_its_level_delay_after_breaking(int level, float capacity, float delay)
    {
        var session = QuietSessionWith((ShipEnhancements.Shield, level));
        Step(session);
        Assert.Equal(capacity, session.Stats.ShieldCapacity);
        Assert.Equal(capacity, session.Stats.ShieldPoints);
        Assert.Equal(0f, session.ShieldRechargeSecondsLeft);

        var toucher = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 1_000_000f, ContactDamage = 30f }, Center);
        session.AddEnemy(toucher);
        Step(session);

        Assert.Equal(0f, session.Stats.ShieldPoints);
        Assert.Equal(delay, session.ShieldRechargeSecondsLeft);
    }

    [Fact]
    public void A_level_1_shield_takes_a_hit_of_8_first_passes_3_to_hp_and_refills_10_s_later_not_before()
    {
        var session = QuietSessionWith((ShipEnhancements.Shield, 1));
        Step(session);
        Assert.Equal(5f, session.Stats.ShieldPoints);

        // A hit of 8 (8.00 this early in the clock): 5 into the shield, the 3 left over off HP.
        var toucher = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 1_000_000f, ContactDamage = 8f }, Center);
        session.AddEnemy(toucher);
        Step(session);
        Assert.Equal(0f, session.Stats.ShieldPoints);
        Assert.Equal(97f, session.Stats.HitPoints);

        // No more hits: still broken just before 10 s, full again at 10 s.
        toucher.TakeDamage(float.MaxValue);
        Step(session, 9.9f);
        Assert.Equal(0f, session.Stats.ShieldPoints);
        Step(session, 0.1f);
        Assert.Equal(5f, session.Stats.ShieldPoints);
        Assert.Equal(97f, session.Stats.HitPoints);
    }

    [Fact]
    public void A_broken_shield_refills_after_its_delay_under_continuous_contact_and_a_level_gained_refills_it()
    {
        var session = QuietSessionWith((ShipEnhancements.Shield, 1));
        // Enough HP to stay alive through 10 s of ticks.
        session.Stats.MaxHitPoints = 1_000f;
        session.Stats.HitPoints = 1_000f;
        var toucher = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 1_000_000f, ContactDamage = 8f }, Center);
        session.AddEnemy(toucher);
        Step(session);
        Assert.Equal(0f, session.Stats.ShieldPoints);
        Assert.Equal(10f, session.ShieldRechargeSecondsLeft);

        // Still touching: the ticks that land on the broken shield do not restart its wait.
        Step(session, GameSession.ContactTickSeconds);
        Assert.Equal(9.5f, session.ShieldRechargeSecondsLeft, precision: 3);

        // Contact goes on, frame by frame; the shield is still broken just before 10 s after the break...
        var sinceBreak = GameSession.ContactTickSeconds;
        while (sinceBreak < 9.9f)
        {
            Step(session);
            sinceBreak += Frame;
        }
        Assert.Equal(0f, session.Stats.ShieldPoints);
        Assert.True(session.ShieldRechargeSecondsLeft is > 0f and < 0.2f,
            $"Wait left {session.ShieldRechargeSecondsLeft} s is not about to run out");

        // ...and refills to capacity once the 10 s run out, though the enemy never stopped touching. A tick due on
        // the refill frame may empty it again at once; that fresh break starts a fresh 10 s wait, proving the refill.
        var refilled = false;
        for (var i = 0; i < 20 && !refilled; i++)
        {
            var hitPointsBefore = session.Stats.HitPoints;
            Step(session);
            refilled = session.Stats.ShieldPoints > 0f || session.ShieldRechargeSecondsLeft > 9f;
            if (refilled && session.Stats.ShieldPoints <= 0f)
                // The refilled 5 points took 5 of the clock-scaled 8-point tick; only the rest reached HP.
                Assert.Equal(EnemyRoster.ScaledContactDamage(8f, session.ElapsedSeconds) - 5f,
                    hitPointsBefore - session.Stats.HitPoints, precision: 2);
        }
        Assert.True(refilled, "A continuously touched shield never refilled");
        Assert.False(session.IsGameOver);

        // Break it (again, if needed) and stop touching for the next part.
        while (session.Stats.ShieldPoints > 0f && !session.IsGameOver)
            Step(session);
        Assert.Equal(0f, session.Stats.ShieldPoints);
        toucher.TakeDamage(float.MaxValue);

        // Levelling the broken shield fills it to its new capacity at once.
        Upgrades.Apply(Upgrade.ForEnhancement(ShipEnhancements.Shield), session.Stats);
        Step(session);
        Assert.Equal(10f, session.Stats.ShieldPoints);
        Assert.Equal(0f, session.ShieldRechargeSecondsLeft);
    }

    // ---- HP stays in [0, Max]; 0 ends the run (CLM-12) ----

    [Fact]
    public void A_lethal_hit_with_second_wind_spent_ends_the_run_at_0_hp_and_freezes_it_despite_regen_and_armor()
    {
        var session = QuietSessionWith((ShipEnhancements.Health, 5), (ShipEnhancements.Regeneration, 5), (ShipEnhancements.Armor, 5));
        // Spend Second Wind: a lethal hit leaves 1 HP and 2 s of immunity.
        session.Stats.HitPoints = 0.5f;
        var first = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 1_000_000f, ContactDamage = 30f }, Center);
        session.AddEnemy(first);
        Step(session);
        Assert.Equal(1f, session.Stats.HitPoints);
        Assert.False(session.IsGameOver);
        first.TakeDamage(float.MaxValue);
        Step(session, 2.1f);
        Assert.Equal(0f, session.InvulnerableSecondsLeft);

        // 30 less armor's 5 is far more than regen gave back: lethal, and Second Wind is gone.
        session.AddEnemy(TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 1_000_000f, ContactDamage = 30f }, Center));
        Step(session);
        Assert.True(session.IsGameOver);
        Assert.Equal(0f, session.Stats.HitPoints);

        var clock = session.ElapsedSeconds;
        for (var i = 0; i < 120; i++)
            Step(session, 0.5f);
        Assert.Equal(0f, session.Stats.HitPoints);
        Assert.Equal(clock, session.ElapsedSeconds);
        Assert.True(session.IsGameOver);
    }

    public static TheoryData<int, int, int> MaxedEnhancementTriples()
    {
        var data = new TheoryData<int, int, int>();
        var count = ShipEnhancements.All.Count;
        for (var a = 0; a < count; a++)
            for (var b = a + 1; b < count; b++)
                for (var c = b + 1; c < count; c++)
                    data.Add(a, b, c);
        return data;
    }

    [Theory]
    [MemberData(nameof(MaxedEnhancementTriples))]
    public void Through_500_frames_of_mixed_contact_with_maxed_enhancements_hp_stays_within_0_and_max(int a, int b, int c)
    {
        // Three slots, so every set of three maxed enhancements (shield, armor, regen, Second Wind, speed) is run.
        var session = QuietSessionWith(
            (ShipEnhancements.All[a], 5), (ShipEnhancements.All[b], 5), (ShipEnhancements.All[c], 5));
        var random = new Random(1000 + a * 100 + b * 10 + c);
        var touchers = new List<Enemy>();

        for (var frame = 0; frame < 500; frame++)
        {
            var roll = random.Next(10);
            if (roll < 3)
            {
                var toucher = TestEnemies.Stationary(EnemyRoster.Gemini with
                {
                    HitPoints = 1_000_000f,
                    ContactDamage = 1f + random.Next(40),
                }, Center + new Vector2(random.Next(-10, 11), random.Next(-10, 11)));
                touchers.Add(toucher);
                session.AddEnemy(toucher);
            }
            else if (roll == 3)
            {
                session.AddHazard(Hazard.CreateBullet(Center, radius: 10f, armSeconds: 0f));
            }
            else if (roll < 6 && touchers.Count > 0)
            {
                var gone = touchers[random.Next(touchers.Count)];
                gone.TakeDamage(float.MaxValue);
                touchers.Remove(gone);
            }

            Step(session, Frame * (1 + random.Next(30)));

            var stats = session.Stats;
            Assert.InRange(stats.HitPoints, 0f, stats.MaxHitPoints);
            Assert.InRange(stats.ShieldPoints, 0f, stats.ShieldCapacity);
            Assert.Equal(stats.HitPoints == 0f, session.IsGameOver);
        }
    }

    // ---- Enhancement cards: 3 slots, level-5 cap (CLM-1) ----

    private static void Own(PlayerStats stats, Enhancement enhancement, int level)
    {
        Upgrades.Apply(Upgrade.NewEnhancement(enhancement), stats);
        for (var l = 1; l < level; l++)
            Upgrades.Apply(Upgrade.ForEnhancement(enhancement), stats);
    }

    [Fact]
    public void Production_offers_hold_new_enhancement_cards_only_while_a_slot_is_free_and_never_a_level_5_enhancement()
    {
        Assert.Equal(ShipEnhancements.All, Upgrades.Enhancements);
        var sawNew = new HashSet<Enhancement>();
        var sawNewWithTwo = false;
        var sawUpgradeWithThree = false;
        for (var seed = 0; seed < 200; seed++)
        {
            // None owned: any of the five may be offered new.
            var none = new PlayerStats();
            none.AddWeapon(WeaponKind.Laser);
            foreach (var card in Upgrades.Offer(none, new Random(seed), GameSession.UpgradeChoices))
                if (card.Kind == UpgradeKind.NewEnhancement)
                    sawNew.Add(card.Enhancement!);

            // Two owned, Move Speed at level 5: new cards only for the three unowned; Move Speed never.
            var two = new PlayerStats();
            two.AddWeapon(WeaponKind.Laser);
            Own(two, ShipEnhancements.MoveSpeed, 5);
            Own(two, ShipEnhancements.Armor, 2);
            var twoOffer = Upgrades.Offer(two, new Random(seed), GameSession.UpgradeChoices);
            Assert.DoesNotContain(twoOffer, card => card.Enhancement == ShipEnhancements.MoveSpeed);
            Assert.DoesNotContain(Upgrade.NewEnhancement(ShipEnhancements.Armor), twoOffer);
            sawNewWithTwo |= twoOffer.Any(card => card.Kind == UpgradeKind.NewEnhancement);

            // Three owned, Shield at level 5: no new-enhancement card at all, and no Shield card.
            var three = new PlayerStats();
            three.AddWeapon(WeaponKind.Laser);
            Own(three, ShipEnhancements.Shield, 5);
            Own(three, ShipEnhancements.Health, 1);
            Own(three, ShipEnhancements.Regeneration, 3);
            var threeOffer = Upgrades.Offer(three, new Random(seed), GameSession.UpgradeChoices);
            Assert.DoesNotContain(threeOffer, card => card.Kind == UpgradeKind.NewEnhancement);
            Assert.DoesNotContain(threeOffer, card => card.Enhancement == ShipEnhancements.Shield);
            Assert.All(threeOffer.Where(card => card.Kind == UpgradeKind.EnhancementUpgrade),
                card => Assert.Contains(card.Enhancement, new[] { ShipEnhancements.Health, ShipEnhancements.Regeneration }));
            sawUpgradeWithThree |= threeOffer.Any(card => card.Kind == UpgradeKind.EnhancementUpgrade);
        }
        Assert.Equal(ShipEnhancements.All.ToHashSet(), sawNew);
        Assert.True(sawNewWithTwo, "With two owned, no offer held a new-enhancement card");
        Assert.True(sawUpgradeWithThree, "With three owned, no offer held an enhancement upgrade");
    }

    [Fact]
    public void Enhancement_card_text_comes_from_ship_enhancements_at_the_level_the_card_brings()
    {
        var stats = new PlayerStats();
        Assert.Equal("ARMOR", Upgrades.Title(Upgrade.NewEnhancement(ShipEnhancements.Armor), stats));
        Assert.Equal(ShipEnhancements.CardDescription(ShipEnhancements.Armor, 1),
            Upgrades.Describe(Upgrade.NewEnhancement(ShipEnhancements.Armor), stats));

        Own(stats, ShipEnhancements.MoveSpeed, 4);
        var toFive = Upgrade.ForEnhancement(ShipEnhancements.MoveSpeed);
        Assert.Equal("MOVE SPEED LV 4 > 5", Upgrades.Title(toFive, stats));
        Assert.Equal(ShipEnhancements.CardDescription(ShipEnhancements.MoveSpeed, 5), Upgrades.Describe(toFive, stats));
        foreach (var enhancement in ShipEnhancements.All)
        {
            var card = Upgrade.NewEnhancement(enhancement);
            Assert.NotEmpty(VectorText.GetStrokes($"{Upgrades.Title(card, new PlayerStats())} {Upgrades.Describe(card, new PlayerStats())}", Vector2.Zero, 10f));
        }
    }

    [Fact]
    public void The_starting_pick_still_offers_exactly_the_five_weapon_cards()
    {
        var session = new GameSession(Center, new Random(3), roster: [], bosses: [], rings: []);
        Assert.Equal(Upgrades.AllWeapons.Select(Upgrade.NewWeapon), session.UpgradeOffer!);
    }

    // ---- Nothing left to offer (CLM-2) ----

    /// <summary>A quiet session owning three level-5 weapons and the given enhancements; the laser kills anything.</summary>
    private static GameSession SessionWithMaxedWeapons(params (Enhancement Enhancement, int Level)[] enhancements)
    {
        var session = QuietSessionWith(enhancements);
        session.Stats.AddWeapon(WeaponKind.Lightning);
        session.Stats.AddWeapon(WeaponKind.Orbs);
        foreach (var weapon in session.Stats.Weapons)
            while (weapon.Level < OwnedWeapon.MaxLevel)
                Upgrades.Apply(Upgrade.ForWeapon(weapon.Kind, Upgrades.OptionsFor(weapon.Kind)[0]), session.Stats);
        session.Stats.Laser().Damage = 1000f;
        return session;
    }

    private static void EarnOneLevel(GameSession session)
    {
        var prize = EnemyRoster.Meta with { Experience = Leveling.Required(session.Leveling.Level) };
        session.AddEnemy(TestEnemies.Stationary(prize, session.Ship.Nose - new Vector2(0, 150)));
        var level = session.Leveling.Level;
        for (var i = 0; i < 60 * 3 && session.Leveling.Level == level; i++)
            Step(session);
        Assert.Equal(level + 1, session.Leveling.Level);
    }

    // With 3 level-5 weapons and 3 level-5 enhancements, PacingTests'
    // Levels_with_no_eligible_card_are_used_up_without_pausing_and_the_queue_keeps_draining proves no card screen shows.

    [Fact]
    public void With_one_upgradable_item_left_a_level_up_offers_exactly_that_card()
    {
        var session = SessionWithMaxedWeapons(
            (ShipEnhancements.MoveSpeed, 5), (ShipEnhancements.Armor, 4), (ShipEnhancements.Regeneration, 5));

        EarnOneLevel(session);

        Assert.True(session.IsChoosingUpgrade);
        Assert.Equal([Upgrade.ForEnhancement(ShipEnhancements.Armor)], session.UpgradeOffer!);
    }

    // ---- Blink (CLM-4) ----

    [Fact]
    public void A_level_5_blink_jumps_250_px_along_forward_then_waits_3_s_for_the_next()
    {
        var session = TestEnemies.QuietSession(new Vector2(Center.X, 700));
        Own(session.Stats, ShipEnhancements.MoveSpeed, 5);
        var start = session.Ship.Position;

        session.RequestBlink();
        Step(session);
        Assert.Equal(ShipEnhancements.BlinkDistance, Vector2.Distance(start, session.Ship.Position), precision: 3);
        Assert.Equal(start + session.Ship.Forward * ShipEnhancements.BlinkDistance, session.Ship.Position);

        // Within the cooldown: the request is dropped.
        var afterFirst = session.Ship.Position;
        for (var i = 0; i < 2 * 60; i++)
            Step(session);
        session.RequestBlink();
        Step(session);
        Assert.Equal(afterFirst, session.Ship.Position);

        // Just short of 3 s since the first blink: still dropped.
        var sinceFirst = (2 * 60 + 1) * Frame;
        Step(session, ShipEnhancements.BlinkCooldownSeconds - sinceFirst - 0.05f);
        session.RequestBlink();
        Step(session, 0.01f);
        Assert.Equal(afterFirst, session.Ship.Position);

        // Past 3 s: accepted.
        Step(session, 0.05f);
        session.RequestBlink();
        Step(session);
        Assert.Equal(ShipEnhancements.BlinkDistance, Vector2.Distance(afterFirst, session.Ship.Position), precision: 3);
    }

    [Fact]
    public void Below_level_5_a_blink_request_leaves_the_ship_in_place()
    {
        var session = QuietSessionWith((ShipEnhancements.MoveSpeed, 4));
        var start = session.Ship.Position;

        session.RequestBlink();
        Step(session);

        Assert.Equal(start, session.Ship.Position);
        Assert.Equal(0f, session.BlinkCooldownSecondsLeft);
    }

    [Fact]
    public void A_blink_toward_an_edge_stops_at_the_edge()
    {
        var session = QuietSessionWith((ShipEnhancements.MoveSpeed, 5));
        // Face right from 100 px short of the right edge.
        session.Update(new Vector2(1, 0), new Vector2(Width + 500, Center.Y), (Width - 100 - Center.X) / (Ship.Speed * ShipEnhancements.SpeedMultiplier(5)), Width, Height);
        var before = session.Ship.Position;
        Assert.Equal(Width - 100, before.X, precision: 2);

        session.RequestBlink();
        session.Update(Vector2.Zero, null, Frame, Width, Height);

        Assert.Equal(Width, session.Ship.Position.X, precision: 3);
        Assert.Equal(before.Y, session.Ship.Position.Y, precision: 3);
    }
}
