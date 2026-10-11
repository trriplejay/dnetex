using System.Numerics;
using SoftwareFactorySurvivors.Core;

namespace SoftwareFactorySurvivors.Core.Tests;

public class EnemyTests
{
    private static readonly Vector2 Start = new(320, 240);

    public static TheoryData<string> RosterNames => new(EnemyRoster.All.Select(type => type.Name));

    [Fact]
    public void Gemini_starts_with_hit_points_and_dies_at_zero()
    {
        var enemy = EnemyRoster.Gemini.Create(Start);
        Assert.Equal(3f, enemy.HitPoints);
        Assert.False(enemy.IsDead);

        enemy.TakeDamage(2f);
        Assert.Equal(1f, enemy.HitPoints);
        Assert.False(enemy.IsDead);

        enemy.TakeDamage(5f);
        Assert.Equal(0f, enemy.HitPoints);
        Assert.True(enemy.IsDead);
    }

    [Fact]
    public void Roster_has_all_ten_logos_with_varied_toughness_speed_and_movement()
    {
        string[] expected = ["Hugging Face", "Gemini", "ChatGPT", "Claude", "Grok", "Copilot", "Cursor", "Perplexity", "Apple Intelligence", "Meta"];
        Assert.Equal(expected, EnemyRoster.All.Select(type => type.Name));

        Assert.True(EnemyRoster.All.Select(type => type.HitPoints).Distinct().Count() >= 6);
        Assert.True(EnemyRoster.All.Select(type => type.Speed).Distinct().Count() >= 6);
        var patterns = EnemyRoster.All.Select(type => type.CreateMovement(type.Speed).GetType()).Distinct().ToArray();
        Assert.True(patterns.Length >= 6, $"Only {patterns.Length} movement patterns");
    }

    [Fact]
    public void Tougher_enemies_are_worth_more_experience()
    {
        var byToughness = EnemyRoster.All.OrderBy(type => type.HitPoints).ToArray();
        for (var i = 1; i < byToughness.Length; i++)
        {
            if (byToughness[i].HitPoints > byToughness[i - 1].HitPoints)
                Assert.True(byToughness[i].Experience >= byToughness[i - 1].Experience,
                    $"{byToughness[i].Name} is tougher than {byToughness[i - 1].Name} but worth less experience");
        }
        Assert.True(EnemyRoster.Meta.Experience > EnemyRoster.HuggingFace.Experience * 10);
    }

    [Theory]
    [MemberData(nameof(RosterNames))]
    public void Each_logo_is_drawn_within_its_radius_and_has_a_hittable_outline(string name)
    {
        var type = EnemyRoster.All.Single(t => t.Name == name);

        var points = type.Outline.SelectMany(stroke => stroke).ToArray();

        Assert.NotEmpty(type.Outline);
        Assert.All(type.Outline, stroke => Assert.True(stroke.Length >= 2));
        Assert.All(points, p => Assert.True(p.Length() <= type.Radius + 0.01f, $"{name} point {p} is outside radius {type.Radius}"));
        Assert.True(points.Max(p => p.Length()) >= type.Radius * 0.8f, $"{name} doesn't fill its radius");

        // A laser fired at the centre from outside must register a hit.
        Assert.True(Collision.ThickSegmentTouches(new Vector2(-type.Radius * 2, 0), new Vector2(0, 0), 1f, type.Outline));
    }

    [Fact]
    public void Spinning_logo_rotates_its_outline_about_its_position()
    {
        var enemy = TestEnemies.Stationary(EnemyRoster.Claude, Start);
        var before = enemy.GetStrokes();

        enemy.Update(Vector2.Zero, 1f);
        var after = enemy.GetStrokes();

        Assert.Equal(EnemyRoster.Claude.SpinSpeed, enemy.Rotation, precision: 5);
        Assert.NotEqual(before[0][1], after[0][1]);
        Assert.Equal(Vector2.Distance(Start, before[0][1]), Vector2.Distance(Start, after[0][1]), precision: 3);
    }

    [Fact]
    public void Chase_moves_toward_the_player_at_its_speed_without_overshooting()
    {
        var chase = new ChasePlayer(90f);

        Assert.Equal(new Vector2(45, 0), chase.Move(Vector2.Zero, new Vector2(100, 0), 0.5f));
        Assert.Equal(new Vector2(100, 0), chase.Move(new Vector2(90, 0), new Vector2(100, 0), 0.5f));
    }

    [Fact]
    public void Weave_closes_in_while_swaying_to_the_side()
    {
        var weave = new Weave(100f, amplitude: 50f, cyclesPerSecond: 1f);
        var position = new Vector2(0, 1000);
        var sideways = new List<float>();

        for (var i = 0; i < 60; i++)
        {
            position = weave.Move(position, Vector2.Zero, 1f / 60f);
            sideways.Add(position.X);
        }

        Assert.InRange(position.Y, 895f, 905f);
        Assert.True(sideways.Max() > 20f && sideways.Min() < 5f);
    }

    [Fact]
    public void Spiral_in_circles_while_slowly_closing_the_distance()
    {
        var spiral = new SpiralIn(100f, offsetDegrees: 60f);
        var position = new Vector2(300, 0);

        var next = spiral.Move(position, Vector2.Zero, 0.1f);

        Assert.Equal(10f, Vector2.Distance(position, next), precision: 3);
        Assert.Equal(300f - 5f, next.Length(), precision: 0);
        Assert.NotEqual(0f, next.Y);
    }

    [Fact]
    public void Dash_creeps_then_lunges_straight_at_where_the_player_was()
    {
        var dash = new Dash(20f, dashSpeed: 400f, creepSeconds: 1f, dashSeconds: 0.5f);
        var position = new Vector2(0, 0);
        var player = new Vector2(1000, 0);

        position = dash.Move(position, player, 0.5f);
        Assert.Equal(10f, position.X, precision: 3);

        position = dash.Move(position, player, 0.5f); // lunge starts and locks direction
        var lungeStart = position;
        position = dash.Move(position, new Vector2(1000, 1000), 0.25f);

        Assert.Equal(100f, position.X - lungeStart.X, precision: 3);
        Assert.Equal(lungeStart.Y, position.Y, precision: 3);
    }

    [Fact]
    public void Blink_teleports_closer_every_interval()
    {
        var blink = new Blink(0f, jumpDistance: 90f, secondsBetweenJumps: 1f);
        var position = new Vector2(500, 0);

        position = blink.Move(position, Vector2.Zero, 0.5f);
        Assert.Equal(500f, position.X);

        position = blink.Move(position, Vector2.Zero, 0.5f);
        Assert.Equal(410f, position.X, precision: 3);
    }

    [Fact]
    public void Accelerate_speeds_up_to_its_top_speed()
    {
        var accelerate = new Accelerate(40f, acceleration: 20f, maxSpeed: 100f);

        accelerate.Move(new Vector2(10000, 0), Vector2.Zero, 1f);
        Assert.Equal(60f, accelerate.CurrentSpeed);

        accelerate.Move(new Vector2(10000, 0), Vector2.Zero, 10f);
        Assert.Equal(100f, accelerate.CurrentSpeed);
    }

    [Fact]
    public void Gemini_outline_is_a_closed_four_pointed_star_with_concave_sides()
    {
        var enemy = EnemyRoster.Gemini.Create(Start);

        var outline = Assert.Single(enemy.GetStrokes());

        Assert.Equal(outline[0], outline[^1]);
        Assert.Contains(outline, p => Vector2.Distance(p, Start + new Vector2(0, -20)) < 0.001f);
        Assert.Contains(outline, p => Vector2.Distance(p, Start + new Vector2(20, 0)) < 0.001f);
        Assert.Contains(outline, p => Vector2.Distance(p, Start + new Vector2(0, 20)) < 0.001f);
        Assert.Contains(outline, p => Vector2.Distance(p, Start + new Vector2(-20, 0)) < 0.001f);

        // Concave: where each side crosses the 45-degree line it sits well inside the straight edge between its tips.
        var sideMidpoint = outline
            .Where(p => p.X > Start.X && p.Y < Start.Y)
            .MinBy(p => MathF.Abs((p.X - Start.X) + (p.Y - Start.Y)));
        Assert.True(Vector2.Distance(sideMidpoint, Start) < 20f / MathF.Sqrt(2f) * 0.75f);
        Assert.True(Collision.ContainsPoint(outline, Start));
    }
}
