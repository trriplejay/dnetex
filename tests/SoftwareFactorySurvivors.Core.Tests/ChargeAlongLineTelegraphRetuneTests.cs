using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class ChargeAlongLineTelegraphRetuneTests
{
    private static readonly Vector2 Far = new(5000f, 0f);

    // Runs the charge to its end and returns the position where the next telegraph starts.
    private static Vector2 FinishCharge(ChargeAlongLine pattern, Vector2 pos)
    {
        for (var i = 0; i < 1000 && pattern.IsCharging; i++)
            pos = pattern.Move(pos, Far, 0.05f);
        Assert.False(pattern.IsCharging);
        return pos;
    }

    [Fact]
    public void Retune_mid_charge_gives_the_next_telegraph_the_new_duration_clamped_to_one_second()
    {
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: 400f, chargeDistance: 700f);
        var pos = pattern.Move(Vector2.Zero, Far, 1.2f);
        Assert.True(pattern.IsCharging);
        pos = pattern.Move(pos, Far, 0.1f);

        pattern.Retune(550f, 900f, telegraphSeconds: 0.5f);

        // Retune itself changes nothing visible: still charging, telegraph value untouched until the next lock.
        Assert.True(pattern.IsCharging);
        Assert.Equal(1.2f, pattern.TelegraphSeconds);

        pos = FinishCharge(pattern, pos);
        Assert.Equal(1.0f, pattern.TelegraphSeconds);

        // The new telegraph lasts exactly 1.0 s, not 1.2 s and not the raw 0.5 s.
        pos = pattern.Move(pos, Far, 0.95f);
        Assert.True(pattern.IsTelegraphing);
        pattern.Move(pos, Far, 0.1f);
        Assert.True(pattern.IsCharging);
    }

    [Fact]
    public void Retune_mid_telegraph_leaves_that_telegraph_at_its_original_duration_and_never_relocks_the_aim()
    {
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: 400f, chargeDistance: 700f);
        var pos = pattern.Move(Vector2.Zero, Far, 0.5f);
        var aimStart = pattern.AimStart;
        var aimEnd = pattern.AimEnd;

        pattern.Retune(550f, 900f, telegraphSeconds: 1.0f);

        Assert.True(pattern.IsTelegraphing);
        Assert.Equal(aimStart, pattern.AimStart);
        Assert.Equal(aimEnd, pattern.AimEnd);

        pos = pattern.Move(pos, Far, 0.69f); // 1.19 s in: the 1.2 s telegraph is still running
        Assert.True(pattern.IsTelegraphing);
        pattern.Move(pos, Far, 0.02f); // 1.21 s in
        Assert.True(pattern.IsCharging);
        Assert.Equal(1.2f, pattern.TelegraphSeconds);
        Assert.Equal(aimStart, pattern.AimStart);
        Assert.Equal(aimEnd, pattern.AimEnd);
    }

    [Fact]
    public void Retune_without_a_telegraph_value_leaves_TelegraphSeconds_unchanged()
    {
        var pattern = new ChargeAlongLine(telegraphSeconds: 1.2f, chargeSpeed: 400f, chargeDistance: 700f);
        var pos = pattern.Move(Vector2.Zero, Far, 1.2f);

        pattern.Retune(550f, 900f);
        FinishCharge(pattern, pos);

        Assert.Equal(1.2f, pattern.TelegraphSeconds);
        Assert.Equal(550f, pattern.ChargeSpeed);
        Assert.Equal(900f, pattern.ChargeDistance);
    }
}
