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
                    DrawThickStroke(stroke, ShipRendering.StrokeWidth, ToColor(ShipRendering.Color));
            }

            foreach (var stroke in _session.Enemies.SelectMany(enemy => enemy.GetStrokes()))
                DrawThickStroke(stroke, EnemyRendering.StrokeWidth, ToColor(EnemyRendering.Color));

            foreach (var laser in _session.Lasers)
                DrawThickStroke(laser.GetLine(), laser.Width, Color.White);
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
        DrawLineStrip([new(left, barTop), new(left + barWidth, barTop), new(left + barWidth, barTop + 10f),
            new(left, barTop + 10f), new(left, barTop)], Color.White);
        var filled = barWidth * leveling.Experience / leveling.ExperienceToNextLevel;
        if (filled > 1f)
            DrawThickStroke([new(left, barTop + 5f), new(left + filled, barTop + 5f)], 8f, ToColor(ShipRendering.Color));

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
            DrawLineStrip([new(left, top), new(right, top), new(right, bottom), new(left, bottom), new(left, top)], Color.White);
            var filled = barHeight * boss.HitPoints / boss.Type.HitPoints;
            if (filled > 1f)
                DrawThickStroke([new(left + barWidth / 2f, bottom), new(left + barWidth / 2f, bottom - filled)], barWidth - 4f, Color.White);
            // Notches where the boss changes behaviour.
            foreach (var phase in boss.Type.Phases ?? [])
            {
                var y = bottom - barHeight * phase.AtHealthFraction;
                DrawLineStrip([new(left - 8f, y), new(left, y)], Color.White);
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
            FillRectangle(topLeft, size, Color.Black);
            DrawThickStroke([topLeft, topLeft + new System.Numerics.Vector2(size.X, 0), topLeft + size,
                topLeft + new System.Numerics.Vector2(0, size.Y), topLeft], 3f, Color.White);

            DrawText($"{i + 1}", new(cardCenter.X, topLeft.Y + 34f), 28f, 3f, ToColor(ShipRendering.Color));
            DrawText(Upgrades.Title(offer[i]), new(cardCenter.X, cardCenter.Y), 18f, 2f, Color.White);
            DrawText(Upgrades.Describe(offer[i], _session.Stats), new(cardCenter.X, topLeft.Y + size.Y - 40f), 14f, 2f, Color.White);
        }

        DrawText("PRESS 1 2 OR 3 OR CLICK A CARD", center + new System.Numerics.Vector2(0, size.Y / 2f + 50f), 16f, 2f, Color.White);
    }

    private void FillRectangle(System.Numerics.Vector2 topLeft, System.Numerics.Vector2 size, Color color)
    {
        VertexPositionColor[] vertices =
        [
            new(new Vector3(topLeft.X, topLeft.Y, 0), color),
            new(new Vector3(topLeft.X + size.X, topLeft.Y, 0), color),
            new(new Vector3(topLeft.X, topLeft.Y + size.Y, 0), color),
            new(new Vector3(topLeft.X + size.X, topLeft.Y + size.Y, 0), color),
        ];
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, 2);
        }
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
        foreach (var stroke in VectorText.GetStrokes(text, center, height))
            DrawThickStroke(stroke, width, color);
    }

    private static Color ToColor(RgbColor rgb) => new(rgb.R, rgb.G, rgb.B);

    private void DrawThickStroke(System.Numerics.Vector2[] stroke, float width, Color color)
    {
        if (stroke.Length < 2)
            return;

        var vertices = StrokeGeometry.Thicken(stroke, width)
            .Select(p => new VertexPositionColor(new Vector3(p.X, p.Y, 0), color))
            .ToArray();

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, vertices.Length - 2);
        }
    }

    private void DrawLineStrip(System.Numerics.Vector2[] stroke, Color color)
    {
        if (stroke.Length < 2)
            return;

        var vertices = stroke
            .Select(p => new VertexPositionColor(new Vector3(p.X, p.Y, 0), color))
            .ToArray();

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineStrip, vertices, 0, vertices.Length - 1);
        }
    }
}
