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
}
