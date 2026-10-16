using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

/// <summary>The weapon-path projectiles: the ballista's shaft and arrowhead, and the spinning bola.</summary>
public class WeaponPathTests
{
    private const float Width = 1000f;
    private const float Height = 800f;
    private static readonly Vector2 Center = new(500, 400);
    private static readonly Vector2 Start = new(300, 300);

    // ---- Ballista ----

    [Fact]
    public void Ballista_shaft_is_four_lasers_long_and_laser_wide_and_its_head_scales_with_it()
    {
        var small = new Ballista(Start, 0f);
        var large = new Ballista(Start, 0f, scale: 2f);

        Assert.Equal(52f, small.ShaftLength);
        Assert.Equal(104f, large.ShaftLength);
        Assert.Equal(4f, small.ShaftWidth);
        Assert.Equal(8f, large.ShaftWidth);

        var smallShaft = Assert.Single(small.GetShape().Lines);
        var largeShaft = Assert.Single(large.GetShape().Lines);
        Assert.Equal(52f, Vector2.Distance(smallShaft.A, smallShaft.B), precision: 3);
        Assert.Equal(104f, Vector2.Distance(largeShaft.A, largeShaft.B), precision: 3);
        Assert.Equal(2f, smallShaft.HalfWidth);
        Assert.Equal(4f, largeShaft.HalfWidth);

        var (smallHeadWidth, smallHeadLength) = HeadExtent(small);
        var (largeHeadWidth, largeHeadLength) = HeadExtent(large);
        Assert.Equal(24f, smallHeadWidth, precision: 3);
        Assert.Equal(12f, smallHeadLength, precision: 3);
        Assert.Equal(2f * smallHeadWidth, largeHeadWidth, precision: 3);
        Assert.Equal(2f * smallHeadLength, largeHeadLength, precision: 3);
    }

    [Fact]
    public void Ballista_arrowhead_is_a_closed_triangle_at_the_leading_end()
    {
        var ballista = new Ballista(Start, 0f);

        var head = Assert.Single(ballista.GetShape().Polygons);
        Assert.Equal(4, head.Length);
        Assert.Equal(head[0], head[^1]);
        // Flying up: the tip is the highest point, 52 + 12 above the tail.
        AssertPointNear(Start + new Vector2(0, -64), head[1]);
    }

    [Fact]
    public void Enemy_touching_only_the_arrowhead_wing_takes_damage()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.WeaponPath = WeaponPath.Ballista;
        // A tiny enemy 2.5 to 3.5 pixels beside the flight line: beyond the shaft's 2-pixel half-width,
        // inside the 6-pixel-wide wing near the head's base.
        var nose = session.Ship.Nose;
        var speck = TestEnemies.Stationary(Speck(new(2.5f, 0), new(3.5f, 0)), new Vector2(nose.X, nose.Y - 150f));
        session.AddEnemy(speck);

        // Small steps so the head can't jump over the speck between frames.
        for (var i = 0; i < 400 && speck.HitPoints == 3f; i++)
            session.Update(Vector2.Zero, null, 1f / 240f, Width, Height);

        Assert.Equal(2f, speck.HitPoints);
    }

    [Fact]
    public void Arrowhead_wing_alone_touches_but_the_shaft_capsule_alone_would_not()
    {
        var ballista = new Ballista(Start, 0f);
        var headBase = Start + new Vector2(0, -52);
        Vector2[][] wing = [[headBase + new Vector2(2.5f, -2f), headBase + new Vector2(3.5f, -2f)]];

        var shaft = ballista.GetShaft();
        Assert.False(Collision.ThickSegmentTouches(shaft[0], shaft[1], ballista.ShaftWidth / 2f, wing));
        Assert.True(ballista.Touches(wing));
    }

    [Fact]
    public void Enemy_touching_only_the_shaft_is_hit()
    {
        var ballista = new Ballista(Start, 0f);
        // Beside the middle of the shaft, within its half-width, far from the head.
        Vector2[][] beside = [[Start + new Vector2(1.5f, -20f), Start + new Vector2(5f, -20f)]];
        Vector2[][] clear = [[Start + new Vector2(2.5f, -20f), Start + new Vector2(5f, -20f)]];

        Assert.True(ballista.Touches(beside));
        Assert.False(ballista.Touches(clear));
    }

    [Fact]
    public void Ballista_is_kept_while_its_head_overlaps_the_screen_and_expires_once_the_head_leaves()
    {
        // Flying right just above the top edge: the shaft's line is off screen, but the 24-pixel-wide
        // head reaches down past y = 0.
        var overlapping = new Ballista(new Vector2(20, -4), MathF.PI / 2f);
        var shaft = overlapping.GetShaft();
        Assert.True(shaft.All(p => p.Y < 0f));
        Assert.False(overlapping.IsExpired(100, 100));

        var gone = new Ballista(new Vector2(20, -13), MathF.PI / 2f);
        Assert.True(gone.IsExpired(100, 100));
    }

    [Fact]
    public void Ballista_tip_alone_on_screen_keeps_it_alive()
    {
        // Diving in from above: shaft entirely above the screen, only the arrowhead's tip below y = 0.
        var entering = new Ballista(new Vector2(50, -60), MathF.PI);
        Assert.False(entering.IsExpired(100, 100));

        var stillAbove = new Ballista(new Vector2(50, -65), MathF.PI);
        Assert.True(stillAbove.IsExpired(100, 100));
    }

    // ---- Bola ----

    [Fact]
    public void Bola_is_two_balls_on_a_laser_wide_line_centred_on_its_position()
    {
        var small = new Bola(Start, 0.3f);
        var large = new Bola(Start, 0.3f, scale: 2f);

        var smallShape = small.GetShape();
        var largeShape = large.GetShape();
        var smallLine = Assert.Single(smallShape.Lines);
        var largeLine = Assert.Single(largeShape.Lines);
        Assert.Equal(4f, smallLine.HalfWidth * 2f);
        Assert.Equal(8f, largeLine.HalfWidth * 2f);

        Assert.Equal(2, smallShape.Circles.Count);
        Assert.Equal(2, largeShape.Circles.Count);
        var smallSpan = Vector2.Distance(smallShape.Circles[0].Center, smallShape.Circles[1].Center);
        var largeSpan = Vector2.Distance(largeShape.Circles[0].Center, largeShape.Circles[1].Center);
        Assert.Equal(6f, small.BallRadius);
        Assert.Equal(12f, large.BallRadius);
        Assert.Equal(52f, smallSpan, precision: 3);
        Assert.Equal(2f * smallSpan, largeSpan, precision: 3);
        Assert.Equal(2f * smallShape.Circles[0].Radius, largeShape.Circles[0].Radius, precision: 3);
        Assert.True(smallShape.Circles[0].Radius > smallLine.HalfWidth);

        AssertPointNear(Start, (smallShape.Circles[0].Center + smallShape.Circles[1].Center) / 2f);
        AssertPointNear(Start, (largeShape.Circles[0].Center + largeShape.Circles[1].Center) / 2f);
        // The connecting line joins the two ball centres.
        AssertPointNear(smallShape.Circles[0].Center, smallLine.A);
        AssertPointNear(smallShape.Circles[1].Center, smallLine.B);
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1.2f, 2f)]
    public void Bola_spins_at_a_fixed_rate_while_its_centre_flies_straight(float heading, float scale)
    {
        var stepped = new Bola(Start, heading, scale);
        var once = new Bola(Start, heading, scale);
        var initial = stepped.Angle;

        stepped.Update(0.1f);
        var afterFirst = stepped.Angle;
        stepped.Update(0.1f);
        once.Update(0.2f);

        AssertAngleNear(initial + Bola.RotationRate * 0.1f, afterFirst);
        AssertAngleNear(initial + Bola.RotationRate * 0.2f, stepped.Angle);
        AssertAngleNear(once.Angle, stepped.Angle);
        Assert.NotEqual(initial, afterFirst);

        var forward = new Vector2(MathF.Sin(heading), -MathF.Cos(heading));
        AssertPointNear(Start + forward * once.Speed * 0.2f, once.Position);
        AssertPointNear(once.Position, stepped.Position);
        Assert.Equal(3 * Ship.Speed, once.Speed);
    }

    [Fact]
    public void Bola_rotation_rate_ignores_heading_and_scale()
    {
        var a = new Bola(Start, 0f);
        var b = new Bola(Start, 2f, scale: 3f);

        a.Update(0.05f);
        b.Update(0.05f);

        AssertAngleNear(0f + Bola.RotationRate * 0.05f, a.Angle);
        AssertAngleNear(2f + Bola.RotationRate * 0.05f, b.Angle);
    }

    [Fact]
    public void Enemy_touching_only_one_ball_or_only_the_line_is_hit()
    {
        // Heading 0 starts the line across the flight path: ball A on the left, ball B on the right, 26 out.
        var bola = new Bola(Start, 0f);
        AssertPointNear(Start - new Vector2(26f, 0), bola.BallA);
        AssertPointNear(Start + new Vector2(26f, 0), bola.BallB);

        // 2.5 below each ball centre: beyond the line's 2-pixel half-width but inside the 6-pixel ball.
        Vector2[][] underA = [[bola.BallA + new Vector2(-0.2f, 2.5f), bola.BallA + new Vector2(0.2f, 2.5f)]];
        Vector2[][] underB = [[bola.BallB + new Vector2(-0.2f, 2.5f), bola.BallB + new Vector2(0.2f, 2.5f)]];
        Vector2[][] middle = [[Start + new Vector2(-0.2f, 1f), Start + new Vector2(0.2f, 1f)]];
        Vector2[][] missed = [[Start + new Vector2(-0.2f, 2.5f), Start + new Vector2(0.2f, 2.5f)]];

        var line = Assert.Single(bola.GetShape().Lines);
        Assert.False(Collision.ThickSegmentTouches(line.A, line.B, line.HalfWidth, underA));
        Assert.False(Collision.ThickSegmentTouches(line.A, line.B, line.HalfWidth, underB));

        Assert.True(bola.Touches(underA));
        Assert.True(bola.Touches(underB));
        Assert.True(bola.Touches(middle));
        Assert.False(bola.Touches(missed));
    }

    [Fact]
    public void Ball_hit_test_follows_the_current_rotation()
    {
        var bola = new Bola(Start, 0f);
        Vector2[][] whereBallBWas = [[bola.BallB + new Vector2(-0.2f, 2.5f), bola.BallB + new Vector2(0.2f, 2.5f)]];
        Assert.True(bola.Touches(whereBallBWas));

        // A quarter turn later (and having flown on), the ball is elsewhere.
        bola.Update(0.125f);
        Assert.False(bola.Touches(whereBallBWas));
        Vector2[][] whereBallBIs = [[bola.BallB + new Vector2(-0.2f, 0.2f), bola.BallB + new Vector2(0.2f, -0.2f)]];
        Assert.True(bola.Touches(whereBallBIs));
    }

    [Fact]
    public void Spinning_bola_damages_an_enemy_it_stays_in_contact_with_only_once()
    {
        var session = TestEnemies.QuietSession(Center);
        session.Stats.WeaponPath = WeaponPath.Bola;
        session.Stats.Pierce = 5;
        var nose = session.Ship.Nose;
        var target = TestEnemies.StationaryGemini(new Vector2(nose.X, nose.Y - 150f));
        session.AddEnemy(target);

        var framesInContact = 0;
        // Run past the first volley's flight through the target but stop before the second volley.
        for (var i = 0; i < 100; i++)
        {
            session.Update(Vector2.Zero, null, 1f / 60f, Width, Height);
            if (session.Lasers.Any(p => p.Touches(target.GetStrokes())))
                framesInContact++;
        }

        Assert.True(framesInContact >= 3, $"Only {framesInContact} frames in contact");
        Assert.Equal(2f, target.HitPoints);
    }

    [Fact]
    public void Bola_is_kept_while_one_ball_overlaps_the_screen_edge()
    {
        // Line along x: ball B at x = -1 reaches 1 pixel onto the screen (at x = 0) with radius 6 (reaches up to x = 5); the line ends at its centre x = -1.
        var overlapping = new Bola(new Vector2(-27, 50), 0f);
        Assert.True(overlapping.BallB.X < 0f);
        Assert.False(overlapping.IsExpired(100, 100));

        var gone = new Bola(new Vector2(-33, 50), 0f);
        Assert.True(gone.IsExpired(100, 100));
    }

    [Fact]
    public void Circle_touches_strokes_within_its_radius_or_around_its_centre()
    {
        Vector2[][] segment = [[new(5, -10), new(5, 10)]];
        Vector2[][] box = [[new(-20, -20), new(20, -20), new(20, 20), new(-20, 20), new(-20, -20)]];

        Assert.True(Collision.CircleTouches(Vector2.Zero, 5f, segment));
        Assert.False(Collision.CircleTouches(Vector2.Zero, 4.9f, segment));
        Assert.True(Collision.CircleTouches(Vector2.Zero, 1f, box));
    }

    private static EnemyType Speck(Vector2 from, Vector2 to) => EnemyRoster.Gemini with { Outline = [[from, to]] };

    private static (float Width, float Length) HeadExtent(Ballista ballista)
    {
        var head = ballista.GetArrowhead();
        var wingSpan = Vector2.Distance(head[0], head[2]);
        var baseMiddle = (head[0] + head[2]) / 2f;
        return (wingSpan, Vector2.Distance(baseMiddle, head[1]));
    }

    private static void AssertAngleNear(float expected, float actual)
    {
        var difference = MathF.IEEERemainder(expected - actual, 2f * MathF.PI);
        Assert.True(MathF.Abs(difference) < 0.0001f, $"Expected angle {expected}, actual {actual}");
    }

    private static void AssertPointNear(Vector2 expected, Vector2 actual) =>
        Assert.True(Vector2.Distance(expected, actual) < 0.001f, $"Expected {expected}, actual {actual}");
}
