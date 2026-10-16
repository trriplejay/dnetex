using SoftwareFactorySurvivors.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SoftwareFactorySurvivors.Desktop;

/// <summary>
/// MonoGame host: reads input, feeds it to the core game logic, and draws the result.
/// </summary>
public class SurvivorsGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private BasicEffect _effect = null!;
    private GameSession _session = null!;

    // Debug start time and level, applied to every new game including restarts.
    private readonly StartOptions _start;

    // Kept for as long as the game runs; a new session doesn't clear it.
    private readonly HighScoreTable _highScores = new();
    private InitialsEntry _initials = new();
    private bool _scoreSaved;
    private int? _savedRank;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;

    // Reused every frame so drawing doesn't allocate per stroke or per glyph.
    private readonly FrameBatch _batch = new();
    private readonly TextStrokeCache _textStrokes = new();
    private VertexPositionColor[] _vertexBuffer = new VertexPositionColor[1024];

    public SurvivorsGame(StartOptions? start = null)
    {
        _start = start ?? StartOptions.Default;
        _graphics = new GraphicsDeviceManager(this);
        Window.Title = "Software Factory Survivors";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // Borderless full screen at the desktop's resolution (no video mode switch).
        var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.PreferredBackBufferWidth = displayMode.Width;
        _graphics.PreferredBackBufferHeight = displayMode.Height;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = true;
        _graphics.ApplyChanges();

        StartSession();
        base.Initialize();
    }

    private void StartSession()
    {
        var viewport = GraphicsDevice.Viewport;
        _session = new GameSession(new System.Numerics.Vector2(viewport.Width / 2f, viewport.Height / 2f), Random.Shared, start: _start);
        _initials = new InitialsEntry();
        _scoreSaved = false;
        _savedRank = null;
    }

    protected override void LoadContent()
    {
        // Draw in pixel coordinates: (0,0) top-left, (width,height) bottom-right.
        var viewport = GraphicsDevice.Viewport;
        _effect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            Projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1),
        };
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape))
            Exit();

        // Edge-triggered so one Enter press can't both save a score and start a new game.
        bool Pressed(Keys key) => keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        _previousMouse = mouse;

        if (_session.UpgradeOffer is { } offer)
        {
            int? choice = null;
            Keys[][] cardKeys = [[Keys.D1, Keys.NumPad1], [Keys.D2, Keys.NumPad2], [Keys.D3, Keys.NumPad3]];
            for (var i = 0; i < offer.Count && i < cardKeys.Length; i++)
            {
                if (cardKeys[i].Any(Pressed))
                    choice = i;
            }
            if (clicked)
                choice ??= UpgradeCards.HitTest(ScreenCenter(), offer.Count, new(mouse.X, mouse.Y));
            if (choice is { } index)
                _session.ChooseUpgrade(index);
        }
        else if (_session.IsVictory && !_scoreSaved)
        {
            for (var key = Keys.A; key <= Keys.Z; key++)
            {
                if (Pressed(key))
                    _initials.Type((char)('A' + (key - Keys.A)));
            }
            if (Pressed(Keys.Back))
                _initials.Backspace();
            if (Pressed(Keys.Enter) && _initials.CanSubmit)
            {
                _savedRank = _highScores.Add(_initials.Text, _session.Score);
                _scoreSaved = true;
            }
        }
        else if (_session.IsFinished && Pressed(Keys.Enter))
        {
            StartSession();
        }

        _previousKeyboard = keyboard;

        var direction = ShipInput.Direction(
            up: keyboard.IsKeyDown(Keys.W), left: keyboard.IsKeyDown(Keys.A),
            down: keyboard.IsKeyDown(Keys.S), right: keyboard.IsKeyDown(Keys.D));
        // The ship snaps to face the mouse every frame, so it turns exactly as fast as the mouse moves.
        var aimTarget = new System.Numerics.Vector2(mouse.X, mouse.Y);

        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var viewport = GraphicsDevice.Viewport;
        _session.Update(direction, aimTarget, elapsedSeconds, viewport.Width, viewport.Height);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        // Thick strokes are triangle strips whose winding alternates, so draw both faces.
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        // Everything below is submitted to the frame batch in painter's order and drawn by one FlushBatch at the end.
        _batch.Reset();

        var viewport = GraphicsDevice.Viewport;
        var center = new System.Numerics.Vector2(viewport.Width / 2f, viewport.Height / 2f);

        // The victory screens replace the play field so the initials and table are easy to read.
        if (!_session.IsVictory)
        {
            // Blink the ship while it's briefly immune after being hit.
            var blinkedOut = _session.InvulnerableSecondsLeft > 0f && (int)(_session.InvulnerableSecondsLeft * 10f) % 2 == 0;
            if (!blinkedOut)
            {
                foreach (var stroke in _session.Ship.GetStrokes())
                    _batch.AddThickStroke(stroke, ShipRendering.StrokeWidth, ShipRendering.Color);
            }

            foreach (var hazard in _session.Hazards)
            {
                switch (hazard.Kind)
                {
                    case HazardKind.Trail:
                        DrawProjectile(hazard.Shape, Color.Orange);
                        break;
                    case HazardKind.Ring:
                        var ringColor = hazard.IsArmed ? Color.Red : Color.DimGray;
                        foreach (var polygon in hazard.Shape.Polygons)
                            _batch.AddThickStroke(polygon, hazard.IsArmed ? 4f : 2f, ToRgb(ringColor));
                        foreach (var line in hazard.Shape.Lines)
                            _batch.AddThickStroke([line.A, line.B], line.HalfWidth * 2f, ToRgb(ringColor));
                        foreach (var circle in hazard.Shape.Circles)
                            _batch.AddCircle(circle.Center, circle.Radius, ToRgb(ringColor));
                        break;
                    case HazardKind.Bullet:
                        DrawProjectile(hazard.Shape, Color.Yellow);
                        break;
                }
            }

            foreach (var enemy in _session.Enemies)
            {
                if (enemy.Movement is ChargeAlongLine charge && charge.IsTelegraphing && charge.AimStart != charge.AimEnd)
                {
                    _batch.AddLineStrip([charge.AimStart, charge.AimEnd], ToRgb(Color.Red * 0.3f));
                }

                var isHallucinationBoss = enemy.Type == EnemyRoster.Hallucination && !enemy.IsCopy;
                var enemyBlinkedOut = isHallucinationBoss && enemy.IsFlickering && (int)(enemy.FlickerSecondsLeft * 15f) % 2 == 0;
                if (!enemyBlinkedOut)
                {
                    foreach (var stroke in enemy.GetStrokes())
                        _batch.AddThickStroke(stroke, EnemyRendering.StrokeWidth, EnemyRendering.Color);
                }
            }

            foreach (var projectile in _session.Lasers)
                DrawProjectile(projectile.GetShape(), Color.White);
        }

        DrawText(GameClock.Format(_session.ElapsedSeconds), new(center.X, 40f), 32f, 3f, Color.White);
        DrawText($"SCORE {_session.Score}", new(center.X, 84f), 16f, 2f, Color.White);
        DrawPlayerStatus();
        if (!_session.IsFinished)
            DrawBossHealth();

        if (_session.UpgradeOffer is { } offer && !_session.IsFinished)
        {
            DrawUpgradeCards(center, offer);
        }
        else if (_session.IsGameOver)
        {
            DrawText("GAME OVER", center, 64f, 4f, Color.White);
            DrawText("PRESS ENTER", center + new System.Numerics.Vector2(0, 80f), 20f, 2f, Color.White);
        }
        else if (_session.IsVictory && !_scoreSaved)
        {
            DrawText("YOU SURVIVED", center + new System.Numerics.Vector2(0, -120f), 64f, 4f, ToColor(ShipRendering.Color));
            DrawText("ENTER YOUR INITIALS", center + new System.Numerics.Vector2(0, -20f), 20f, 2f, Color.White);
            DrawText(_initials.Text.PadRight(InitialsEntry.MaxLength, '_'), center + new System.Numerics.Vector2(0, 50f), 48f, 4f, Color.White);
            DrawText("PRESS ENTER TO SAVE", center + new System.Numerics.Vector2(0, 130f), 16f, 2f, Color.White);
        }
        else if (_session.IsVictory)
        {
            DrawHighScores(center);
        }

        FlushBatch();
        base.Draw(gameTime);
    }

    private System.Numerics.Vector2 ScreenCenter()
    {
        var viewport = GraphicsDevice.Viewport;
        return new(viewport.Width / 2f, viewport.Height / 2f);
    }

    /// <summary>Level, experience bar and hit points in the top-left corner.</summary>
    private void DrawPlayerStatus()
    {
        const float left = 32f;
        const float barWidth = 220f;
        var leveling = _session.Leveling;
        var stats = _session.Stats;

        DrawTextLeft($"LV {leveling.Level}", new(left, 36f), 24f, 2f, ToColor(ShipRendering.Color));

        var barTop = 62f;
        _batch.AddLineStrip([new(left, barTop), new(left + barWidth, barTop), new(left + barWidth, barTop + 10f),
            new(left, barTop + 10f), new(left, barTop)], ToRgb(Color.White));
        var filled = barWidth * leveling.Experience / leveling.ExperienceToNextLevel;
        if (filled > 1f)
            _batch.AddThickStroke([new(left, barTop + 5f), new(left + filled, barTop + 5f)], 8f, ShipRendering.Color);

        DrawTextLeft($"HP {stats.HitPoints}/{stats.MaxHitPoints}", new(left, 96f), 16f, 2f, Color.White);
    }

    /// <summary>Each living boss's name and a health bar, under the clock.</summary>
    /// <summary>A tall health bar per living boss down the right side of the screen, draining from the top, notched at its phases.</summary>
    private void DrawBossHealth()
    {
        const float barWidth = 16f;
        const float rightMargin = 40f;
        const float labelHeight = 16f;
        var viewport = GraphicsDevice.Viewport;
        var barHeight = viewport.Height * 0.6f;
        var top = (viewport.Height - barHeight) / 2f;
        var bottom = top + barHeight;
        var right = viewport.Width - rightMargin;

        foreach (var boss in _session.Bosses)
        {
            var left = right - barWidth;
            var name = boss.Type.Name.ToUpperInvariant();
            DrawTextLeft(name, new(right - VectorText.MeasureWidth(name, labelHeight), top - 24f), labelHeight, 2f, Color.White);
            _batch.AddLineStrip([new(left, top), new(right, top), new(right, bottom), new(left, bottom), new(left, top)], ToRgb(Color.White));
            var filled = barHeight * boss.HitPoints / boss.Type.HitPoints;
            if (filled > 1f)
                // Greyed out while its shield makes it immune to damage.
                _batch.AddThickStroke([new(left + barWidth / 2f, bottom), new(left + barWidth / 2f, bottom - filled)], barWidth - 4f,
                    ToRgb(boss.IsShielded ? Color.Gray : Color.White));
            // Notches where the boss changes behaviour.
            foreach (var phase in boss.Type.Phases ?? [])
            {
                var y = bottom - barHeight * phase.AtHealthFraction;
                _batch.AddLineStrip([new(left - 8f, y), new(left, y)], ToRgb(Color.White));
            }
            right -= barWidth + 48f;
        }
    }

    private void DrawUpgradeCards(System.Numerics.Vector2 center, IReadOnlyList<Upgrade> offer)
    {
        DrawText("LEVEL UP", center + new System.Numerics.Vector2(0, -UpgradeCards.CardSize.Y / 2f - 70f), 40f, 3f, ToColor(ShipRendering.Color));

        var corners = UpgradeCards.Layout(center, offer.Count);
        var size = UpgradeCards.CardSize;
        for (var i = 0; i < offer.Count; i++)
        {
            var topLeft = corners[i];
            var cardCenter = topLeft + size / 2f;

            // A solid black face hides the frozen play field behind the card.
            // As a closed outline filled as a fan: the same two triangles' area as the old 4-vertex strip.
            var topRight = topLeft + new System.Numerics.Vector2(size.X, 0);
            var bottomLeft = topLeft + new System.Numerics.Vector2(0, size.Y);
            _batch.AddPolygon([topLeft, topRight, topLeft + size, bottomLeft, topLeft], ToRgb(Color.Black));
            _batch.AddThickStroke([topLeft, topRight, topLeft + size, bottomLeft, topLeft], 3f, ToRgb(Color.White));

            DrawText($"{i + 1}", new(cardCenter.X, topLeft.Y + 34f), 28f, 3f, ToColor(ShipRendering.Color));
            DrawText(Upgrades.Title(offer[i]), new(cardCenter.X, cardCenter.Y), 18f, 2f, Color.White);
            DrawText(Upgrades.Describe(offer[i], _session.Stats), new(cardCenter.X, topLeft.Y + size.Y - 40f), 14f, 2f, Color.White);
        }

        DrawText($"PRESS {CardKeysPrompt(offer.Count)} OR CLICK A CARD", center + new System.Numerics.Vector2(0, size.Y / 2f + 50f), 16f, 2f, Color.White);
    }

    /// <summary>"1", "1 OR 2", "1 2 OR 3": only the keys for the cards actually shown.</summary>
    private static string CardKeysPrompt(int count) => count switch
    {
        <= 1 => "1",
        _ => string.Join(" ", Enumerable.Range(1, count - 1)) + $" OR {count}",
    };

    /// <summary>Draws the same Core shape the projectile is hit-tested with: thick lines, filled polygons and discs.</summary>
    /// <remarks>Submits to the frame batch (same triangles as the old per-piece fills); Draw flushes it.</remarks>
    private void DrawProjectile(ProjectileShape shape, Color color)
    {
        var rgb = ToRgb(color);
        foreach (var line in shape.Lines)
            _batch.AddThickStroke([line.A, line.B], line.HalfWidth * 2f, rgb);
        foreach (var polygon in shape.Polygons)
            _batch.AddPolygon(polygon, rgb);
        foreach (var circle in shape.Circles)
            _batch.AddCircle(circle.Center, circle.Radius, rgb);
    }

    private void DrawTextLeft(string text, System.Numerics.Vector2 leftCenter, float height, float width, Color color) =>
        DrawText(text, leftCenter + new System.Numerics.Vector2(VectorText.MeasureWidth(text, height) / 2f, 0), height, width, color);

    private void DrawHighScores(System.Numerics.Vector2 center)
    {
        const float rowSpacing = 36f;
        var top = center.Y - (_highScores.Entries.Count + 3) * rowSpacing / 2f;

        DrawText("HIGH SCORES", new(center.X, top), 40f, 3f, ToColor(ShipRendering.Color));
        for (var rank = 0; rank < _highScores.Entries.Count; rank++)
        {
            var entry = _highScores.Entries[rank];
            var row = $"{rank + 1,2}. {entry.Initials,-3} {entry.Score,5}";
            var color = rank == _savedRank ? ToColor(ShipRendering.Color) : Color.White;
            DrawText(row, new(center.X, top + (rank + 2) * rowSpacing), 22f, 2f, color);
        }
        DrawText("PRESS ENTER", new(center.X, top + (_highScores.Entries.Count + 3) * rowSpacing), 20f, 2f, Color.White);
    }

    private void DrawText(string text, System.Numerics.Vector2 center, float height, float width, Color color)
    {
        var rgb = ToRgb(color);
        foreach (var stroke in _textStrokes.GetStrokes(text, center, height))
            _batch.AddThickStroke(stroke, width, rgb);
    }

    /// <summary>Draws everything submitted to the batch, one draw call per batch, in submission order, then resets it.</summary>
    private void FlushBatch()
    {
        for (var b = 0; b < _batch.BatchCount; b++)
        {
            var batch = _batch.GetBatch(b);
            var source = _batch.GetVertices(batch);
            if (_vertexBuffer.Length < source.Length)
                Array.Resize(ref _vertexBuffer, Math.Max(source.Length, _vertexBuffer.Length * 2));
            for (var i = 0; i < source.Length; i++)
            {
                var v = source[i];
                _vertexBuffer[i] = new VertexPositionColor(new Vector3(v.Position.X, v.Position.Y, 0), ToColor(v.Color));
            }

            var (type, primitives) = batch.Kind == BatchKind.Triangles
                ? (PrimitiveType.TriangleList, source.Length / 3)
                : (PrimitiveType.LineList, source.Length / 2);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(type, _vertexBuffer, 0, primitives);
            }
        }
        _batch.Reset();
    }

    private static RgbColor ToRgb(Color color) => new(color.R, color.G, color.B);

    private static Color ToColor(RgbColor rgb) => new(rgb.R, rgb.G, rgb.B);
}
