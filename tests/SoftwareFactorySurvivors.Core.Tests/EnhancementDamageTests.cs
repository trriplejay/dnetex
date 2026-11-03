using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>
/// How the ship's enhancements change a hit: armor and its level-5 reflection, the level-5 shield's blast,
/// Health's Second Wind, plus the regeneration card text and ShipEnhancements owning every number.
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

    [Fact]
    public void Level_5_armor_deals_what_it_blocked_to_the_attacker_and_a_kill_by_it_is_credited()
    {
        // Raw 5, armor blocks 4: an attacker with 10 HP loses 4.
        var session = SessionWith(ShipEnhancements.Armor, 5);
        var attacker = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 10f }, Center);
        session.AddEnemy(attacker);
        Step(session);
        Assert.Equal(6f, attacker.HitPoints);
        Assert.Equal(99f, session.Stats.HitPoints);

        // An attacker with 3 HP dies from the 4 reflected, and the kill gives score and experience.
        session = SessionWith(ShipEnhancements.Armor, 5);
        var weak = TestEnemies.Stationary(EnemyRoster.Gemini, Center);
        session.AddEnemy(weak);
        Step(session);
        Assert.True(weak.IsDead);
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(EnemyRoster.Gemini.Experience, session.Leveling.Experience);
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

    // ---- Level-5 shield blast (CLM-9) ----

    [Fact]
    public void Breaking_a_level_5_shield_deals_25_to_every_enemy_within_300_px_once()
    {
        var session = SessionWith(ShipEnhancements.Shield, 5);
        var near = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 30f }, Center + new Vector2(200, 0));
        var far = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 30f }, Center + new Vector2(400, 0));
        session.AddEnemy(near);
        session.AddEnemy(far);
        // A 30-damage hit empties the 25-point shield.
        session.AddEnemy(TestEnemies.Stationary(Toucher with { ContactDamage = 30f }, Center));

        Step(session);

        Assert.Equal(0f, session.ShieldPoints);
        Assert.Equal(95f, session.Stats.HitPoints);
        Assert.Equal(5f, near.HitPoints);
        Assert.Equal(30f, far.HitPoints);
        Assert.Equal(0, session.Score);

        // The broken shield blasts no more on later hits.
        Step(session, 40);
        Assert.Equal(5f, near.HitPoints);
    }

    [Fact]
    public void A_level_5_shield_blast_kills_are_credited_and_a_level_4_break_damages_nothing()
    {
        var session = SessionWith(ShipEnhancements.Shield, 5);
        var near = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 20f }, Center + new Vector2(200, 0));
        session.AddEnemy(near);
        session.AddEnemy(TestEnemies.Stationary(Toucher with { ContactDamage = 30f }, Center));
        Step(session);
        Assert.True(near.IsDead);
        Assert.Equal(GameSession.PointsPerKill, session.Score);
        Assert.Equal(EnemyRoster.Gemini.Experience, session.Leveling.Experience);

        session = SessionWith(ShipEnhancements.Shield, 4);
        var near4 = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 30f }, Center + new Vector2(200, 0));
        var far4 = TestEnemies.Stationary(EnemyRoster.Gemini with { HitPoints = 30f }, Center + new Vector2(400, 0));
        session.AddEnemy(near4);
        session.AddEnemy(far4);
        session.AddEnemy(TestEnemies.Stationary(Toucher with { ContactDamage = 30f }, Center));
        Step(session);
        Assert.Equal(0f, session.ShieldPoints);
        Assert.Equal(90f, session.Stats.HitPoints);
        Assert.Equal(30f, near4.HitPoints);
        Assert.Equal(30f, far4.HitPoints);
    }

    // ---- Second Wind (CLM-6) ----

    [Fact]
    public void Second_wind_leaves_1_hp_and_2_s_of_immunity_once_then_a_lethal_hit_ends_the_game()
    {
        var session = SessionWith(ShipEnhancements.Health, 5);
        session.Stats.HitPoints = 3f;
        session.AddEnemy(TestEnemies.Stationary(Toucher, Center));

        Step(session);
        Assert.Equal(1f, session.Stats.HitPoints);
        Assert.False(session.IsGameOver);
        Assert.Equal(2f, session.InvulnerableSecondsLeft, precision: 3);

        // Still touching through the 2 s of immunity: nothing lands.
        var frames = 0;
        while (session.InvulnerableSecondsLeft > 0f && frames < 200)
        {
            Step(session);
            frames++;
            if (session.InvulnerableSecondsLeft > 0f)
                Assert.Equal(1f, session.Stats.HitPoints);
        }
        Assert.InRange(frames * Frame, 1.95f, 2.05f);

        // Once it runs out, the next lethal tick ends the run: Second Wind is used up.
        for (var i = 0; i < 60 && !session.IsGameOver; i++)
            Step(session);
        Assert.True(session.IsGameOver);
        Assert.Equal(0f, session.Stats.HitPoints);
    }

    [Fact]
    public void Below_health_level_5_a_lethal_hit_ends_the_game_at_once()
    {
        var session = SessionWith(ShipEnhancements.Health, 4);
        session.Stats.HitPoints = 3f;
        session.AddEnemy(TestEnemies.Stationary(Toucher, Center));

        Step(session);

        Assert.True(session.IsGameOver);
        Assert.Equal(0f, session.Stats.HitPoints);
        Assert.Equal(0f, session.InvulnerableSecondsLeft);
    }

    // ---- Card text (CLM-11) ----

    [Fact]
    public void Regeneration_card_states_n_hp_per_5_seconds()
    {
        Assert.Equal("+3 HP PER 5 SECONDS", ShipEnhancements.CardDescription(ShipEnhancements.Regeneration, 3));
        Assert.Equal("+1 HP PER 5 SECONDS", ShipEnhancements.CardDescription(ShipEnhancements.Regeneration, 1));
        Assert.StartsWith("+5 HP PER 5 SECONDS", ShipEnhancements.CardDescription(ShipEnhancements.Regeneration, 5));
        Assert.Equal(0.6f, ShipEnhancements.RegenPerTick(3), precision: 4);
    }

    [Fact]
    public void Every_enhancement_card_names_its_level_values_and_only_the_level_5_card_names_the_bonus()
    {
        Assert.Equal("+20% MOVE SPEED", ShipEnhancements.CardDescription(ShipEnhancements.MoveSpeed, 2));
        Assert.Equal("+60 MAX HP", ShipEnhancements.CardDescription(ShipEnhancements.Health, 3));
        Assert.Equal("20 SHIELD, REFILLS 7 S AFTER BREAKING", ShipEnhancements.CardDescription(ShipEnhancements.Shield, 4));
        Assert.Equal("-2 DAMAGE PER HIT, NEVER BELOW 1", ShipEnhancements.CardDescription(ShipEnhancements.Armor, 2));
        foreach (var enhancement in ShipEnhancements.All)
        {
            Assert.DoesNotContain("LV5", ShipEnhancements.CardDescription(enhancement, 4));
            var top = ShipEnhancements.CardDescription(enhancement, 5);
            Assert.Contains("LV5", top);
            // Every character is one the vector font can draw.
            VectorText.GetStrokes(top, Vector2.Zero, 10f);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => ShipEnhancements.CardDescription(ShipEnhancements.Armor, 6));
        Assert.Throws<ArgumentException>(() => ShipEnhancements.CardDescription(new Enhancement("OTHER", "X"), 1));
    }

    // ---- One owner of the numbers (CLM-13) ----

    [Fact]
    public void ShipEnhancements_owns_the_bonus_numbers_and_the_session_holds_no_copies()
    {
        Assert.Equal(250f, ShipEnhancements.BlinkDistance);
        Assert.Equal(3f, ShipEnhancements.BlinkCooldownSeconds);
        Assert.Equal(300f, ShipEnhancements.ShieldExplosionRadius);
        Assert.Equal(25f, ShipEnhancements.ShieldExplosionDamage);
        Assert.Equal(2f, ShipEnhancements.SecondWindSeconds);
        Assert.Equal(5f, ShipEnhancements.RegenBoostSeconds);
        Assert.Equal([10f, 9f, 8f, 7f, 5f], Enumerable.Range(1, 5).Select(ShipEnhancements.ShieldRechargeSeconds));
        Assert.Equal([5f, 10f, 15f, 20f, 25f], Enumerable.Range(1, 5).Select(ShipEnhancements.ShieldCapacity));
        Assert.Equal([20f, 40f, 60f, 80f, 100f], Enumerable.Range(1, 5).Select(ShipEnhancements.HealthBonus));
        Assert.Equal([1f, 2f, 3f, 4f, 5f], Enumerable.Range(1, 5).Select(ShipEnhancements.ArmorReduction));

        // Any line of the session or the stats that touches an enhancement carries no number but 0: each value is
        // read from ShipEnhancements by name.
        var enhancementLine = new Regex(@"ShipEnhancements|Shield|Armor|SecondWind|Regen|Blink|SpeedMultiplier");
        var numberOtherThanZero = new Regex(@"(?<![\w.])(?!0(\.0+)?f?\b)\d+(\.\d+)?f?\b");
        foreach (var file in new[] { "GameSession.cs", "PlayerStats.cs" })
        {
            var offending = File.ReadLines(Path.Combine(CoreDirectory(), file))
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(line => enhancementLine.IsMatch(line) && numberOtherThanZero.IsMatch(line))
                .ToArray();
            Assert.True(offending.Length == 0, $"{file} holds enhancement numbers: {string.Join(" | ", offending)}");
        }
        // The session reads the bonus numbers it uses from ShipEnhancements.
        var session = File.ReadAllText(Path.Combine(CoreDirectory(), "GameSession.cs"));
        foreach (var member in new[] { "ShieldExplosionRadius", "ShieldExplosionDamage", "SecondWindSeconds", "SecondWindHitPoints", "ArmoredDamage", "SpeedMultiplier(" })
            Assert.Contains("ShipEnhancements." + member, session);
    }

    private static string CoreDirectory([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "src", "SoftwareFactorySurvivors.Core"));
}
