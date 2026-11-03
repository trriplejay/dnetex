using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>
/// How the ship's enhancements change a hit (armor, shield breaking, lethal hits at any Health level), plus the
/// card text and ShipEnhancements owning every number.
/// </summary>
public class EnhancementDamageTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private const float Frame = 1f / 60f;
    private static readonly Vector2 Center = new(500, 400);

    // A Gemini that takes much to kill and hits for 5 (5.00 at the clock's first frames).
    private static readonly EnemyType Toucher = EnemyRoster.Gemini with { HitPoints = 1_000_000f };

    private static GameSession SessionWith(Enhancement enhancement, int level)
    {
        var session = TestEnemies.QuietSession(Center);
        var owned = session.Stats.AddEnhancement(enhancement);
        while (owned.Level < level)
            owned.RaiseLevel();
        return session;
    }

    private static void Step(GameSession session, int frames = 1)
    {
        for (var i = 0; i < frames; i++)
            session.Update(Vector2.Zero, null, Frame, Width, Height);
    }

    // ---- Armor (CLM-10) ----

    [Theory]
    [InlineData(0, 95f)]
    [InlineData(3, 98f)]
    [InlineData(4, 99f)]
    [InlineData(5, 99f)]
    public void Armor_takes_its_level_off_a_hit_but_never_below_1(int level, float hitPointsAfter)
    {
        var session = level == 0 ? TestEnemies.QuietSession(Center) : SessionWith(ShipEnhancements.Armor, level);
        session.AddEnemy(TestEnemies.Stationary(Toucher, Center));

        Step(session);

        Assert.Equal(hitPointsAfter, session.Stats.HitPoints);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(20)]
    public void Armor_at_level_5_or_20_blocks_a_contact_hit_but_deals_nothing_back_to_the_attacker(int level)
    {
        var session = SessionWith(ShipEnhancements.Armor, level);
        var attacker = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 10f }, Center);
        session.AddEnemy(attacker);

        Step(session);

        // Raw 5 is floored at 1 by the armor; the 4 blocked go nowhere.
        Assert.Equal(99f, session.Stats.HitPoints);
        Assert.Equal(10f, attacker.HitPoints);
        Assert.Equal(0, session.Score);
    }

    [Fact]
    public void Level_4_armor_reflects_nothing_and_hazards_take_no_reflection()
    {
        var session = SessionWith(ShipEnhancements.Armor, 4);
        var attacker = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 10f }, Center);
        session.AddEnemy(attacker);
        Step(session);
        Assert.Equal(10f, attacker.HitPoints);
        Assert.Equal(0, session.Score);

        // A trail ticking on a level-5 armored ship is reduced by armor and harms no enemy.
        session = SessionWith(ShipEnhancements.Armor, 5);
        var bystander = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 10f }, Center + new Vector2(200, 0));
        session.AddEnemy(bystander);
        // Trails arm after 1 s; 70 frames cover the arming tick and stop before the next one is due.
        session.AddHazard(Hazard.CreateTrail(Center + new Vector2(-50, 0), Center + new Vector2(50, 0)));
        Step(session, 70);
        Assert.Equal(10f, bystander.HitPoints);
        Assert.Equal(0, session.Score);
        // Raw 4, armor 5: floored at 1.
        Assert.Equal(99f, session.Stats.HitPoints);
    }

    // ---- Shield breaking ----

    [Fact]
    public void Breaking_a_level_20_shield_damages_no_enemy_and_credits_no_kill()
    {
        var session = SessionWith(ShipEnhancements.Shield, 20);
        Assert.Equal(100f, session.ShieldCapacity);
        var near = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 1f, Experience = 1 }, Center + new Vector2(100, 0));
        session.AddEnemy(near);
        // A 150-damage hit empties the 100-point shield.
        session.AddEnemy(TestEnemies.Stationary(Toucher with { ContactDamage = 150f }, Center));

        Step(session);

        Assert.Equal(0f, session.ShieldPoints);
        Assert.True(session.Stats.HitPoints < 100f);
        Assert.Equal(1f, near.HitPoints);
        Assert.False(near.IsDead);
        Assert.Equal(0, session.Score);
        Assert.Equal(0, session.Leveling.Experience);
    }

    // ---- Lethal hits ----

    [Fact]
    public void With_health_at_level_20_a_lethal_hit_ends_the_run_at_once_at_0_hp()
    {
        var session = SessionWith(ShipEnhancements.Health, 20);
        session.Stats.HitPoints = 3f;
        session.AddEnemy(TestEnemies.Stationary(Toucher, Center));

        Step(session);

        Assert.True(session.IsGameOver);
        Assert.Equal(0f, session.Stats.HitPoints);
    }

    // ---- Second Wind (CLM-6) ----

    // ---- Card text (CLM-11) ----

    [Fact]
    public void Regeneration_card_states_n_hp_per_5_seconds()
    {
        Assert.Equal("+3 HP PER 5 SECONDS", ShipEnhancements.CardDescription(ShipEnhancements.Regeneration, 3));
        Assert.Equal("+1 HP PER 5 SECONDS", ShipEnhancements.CardDescription(ShipEnhancements.Regeneration, 1));
        Assert.Equal("+5 HP PER 5 SECONDS", ShipEnhancements.CardDescription(ShipEnhancements.Regeneration, 5));
        Assert.Equal(0.6f, ShipEnhancements.RegenPerTick(3), precision: 4);
    }

    [Fact]
    public void Every_enhancement_card_names_its_level_values_and_no_card_from_level_1_to_20_names_a_level_5_bonus()
    {
        Assert.Equal("+20% MOVE SPEED", ShipEnhancements.CardDescription(ShipEnhancements.MoveSpeed, 2));
        Assert.Equal("+60 MAX HP", ShipEnhancements.CardDescription(ShipEnhancements.Health, 3));
        Assert.Equal("20 SHIELD, REFILLS 8.5 S AFTER BREAKING", ShipEnhancements.CardDescription(ShipEnhancements.Shield, 4));
        Assert.Equal("-2 DAMAGE PER HIT, NEVER BELOW 1", ShipEnhancements.CardDescription(ShipEnhancements.Armor, 2));
        Assert.Equal("+50% MOVE SPEED", ShipEnhancements.CardDescription(ShipEnhancements.MoveSpeed, 5));
        Assert.Equal("+100 MAX HP", ShipEnhancements.CardDescription(ShipEnhancements.Health, 5));
        Assert.Equal("25 SHIELD, REFILLS 8 S AFTER BREAKING", ShipEnhancements.CardDescription(ShipEnhancements.Shield, 5));
        Assert.Equal("-5 DAMAGE PER HIT, NEVER BELOW 1", ShipEnhancements.CardDescription(ShipEnhancements.Armor, 5));
        foreach (var enhancement in ShipEnhancements.All)
        {
            for (var level = 1; level <= OwnedEnhancement.MaxLevel; level++)
            {
                var card = ShipEnhancements.CardDescription(enhancement, level);
                Assert.DoesNotContain("LV5", card);
                Assert.DoesNotContain("LV 5", card);
                Assert.DoesNotContain("BLINK", card);
                Assert.DoesNotContain("SECOND WIND", card);
                Assert.DoesNotContain("REFLECT", card);
                Assert.DoesNotContain("DOUBLED", card);
                Assert.DoesNotContain("BREAKING DEALS", card);
                // Every character is one the vector font can draw.
                VectorText.GetStrokes(card, Vector2.Zero, 10f);
            }
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipEnhancements.CardDescription(ShipEnhancements.Armor, 21));
        Assert.Throws<ArgumentException>(() => ShipEnhancements.CardDescription(new Enhancement("OTHER", "X"), 1));
    }

    // ---- One owner of the numbers (CLM-13) ----

    [Fact]
    public void ShipEnhancements_owns_the_numbers_and_the_session_holds_no_copies()
    {
        // The shield wait has one owner with 20 entries, 10 - 0.5*(level-1), and no extra drop at level 5.
        var expectedDelays = Enumerable.Range(1, 20).Select(l => 10f - 0.5f * (l - 1)).ToArray();
        Assert.Equal(expectedDelays, Enumerable.Range(1, 20).Select(ShipEnhancements.ShieldRechargeSeconds));
        Assert.Equal(8f, ShipEnhancements.ShieldRechargeSeconds(5));
        Assert.Equal(0.5f, ShipEnhancements.ShieldRechargeSeconds(20));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipEnhancements.ShieldRechargeSeconds(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipEnhancements.ShieldRechargeSeconds(21));
        // The shield card reads its wait from that owner at every level.
        for (var level = 1; level <= OwnedEnhancement.MaxLevel; level++)
        {
            var wait = ShipEnhancements.ShieldRechargeSeconds(level).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Contains($"REFILLS {wait} S AFTER BREAKING", ShipEnhancements.CardDescription(ShipEnhancements.Shield, level));
        }
        Assert.Equal([5f, 10f, 15f, 20f, 25f], Enumerable.Range(1, 5).Select(ShipEnhancements.ShieldCapacity));
        Assert.Equal([20f, 40f, 60f, 80f, 100f], Enumerable.Range(1, 5).Select(ShipEnhancements.HealthBonus));
        Assert.Equal([1f, 2f, 3f, 4f, 5f], Enumerable.Range(1, 5).Select(ShipEnhancements.ArmorReduction));

        // Any line of the session or the stats that touches an enhancement carries no number but 0: each value is
        // read from ShipEnhancements by name.
        var enhancementLine = new Regex(@"ShipEnhancements|Shield|Armor|Regen|SpeedMultiplier");
        var numberOtherThanZero = new Regex(@"(?<![\w.])(?!0(\.0+)?f?\b)\d+(\.\d+)?f?\b");
        foreach (var file in new[] { "GameSession.cs", "PlayerStats.cs" })
        {
            var offending = File.ReadLines(Path.Combine(CoreDirectory(), file))
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(line => enhancementLine.IsMatch(line) && numberOtherThanZero.IsMatch(line))
                .ToArray();
            Assert.True(offending.Length == 0, $"{file} holds enhancement numbers: {string.Join(" | ", offending)}");
        }
        // The session reads the numbers it uses from ShipEnhancements.
        var session = File.ReadAllText(Path.Combine(CoreDirectory(), "GameSession.cs"));
        foreach (var member in new[] { "ShieldRechargeSeconds(", "RegenPerTick(", "ArmoredDamage", "SpeedMultiplier(" })
            Assert.Contains("ShipEnhancements." + member, session);
    }

    private static string CoreDirectory([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "src", "SoftwareFactorySurvivors.Core"));
}
