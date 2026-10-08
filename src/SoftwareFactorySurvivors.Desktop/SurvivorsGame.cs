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
    private Ship _ship = null!;
    private readonly FiringTimer _firingTimer = new();
    private readonly List<Laser> _lasers = [];

    public SurvivorsGame()
    {
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

        var viewport = GraphicsDevice.Viewport;
        _ship = new Ship(new System.Numerics.Vector2(viewport.Width / 2f, viewport.Height / 2f));
        base.Initialize();
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

        var turn = 0f;
        var thrust = 0f;
        if (keyboard.IsKeyDown(Keys.A)) turn += ShipInput.KeyToTurn(MovementKey.A);
        if (keyboard.IsKeyDown(Keys.D)) turn += ShipInput.KeyToTurn(MovementKey.D);
        if (keyboard.IsKeyDown(Keys.W)) thrust += ShipInput.KeyToThrust(MovementKey.W);
        if (keyboard.IsKeyDown(Keys.S)) thrust += ShipInput.KeyToThrust(MovementKey.S);

        var elapsedSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _ship.Update(turn, thrust, elapsedSeconds);

        foreach (var laser in _lasers)
            laser.Update(elapsedSeconds);

        _lasers.AddRange(_firingTimer.Update(elapsedSeconds, _ship.Nose, _ship.Heading));
        var viewport = GraphicsDevice.Viewport;
        _lasers.RemoveAll(laser => laser.IsExpired(viewport.Width, viewport.Height));

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        var shipStrokes = _ship.GetStrokes();

        for (var strokeIndex = 0; strokeIndex < shipStrokes.Count; strokeIndex++)
        {
            var stroke = shipStrokes[strokeIndex];
            if (stroke.Length < 2)
                continue;

            var vertices = stroke
                .Select((p, vertexIndex) =>
                {
                    var rgb = ShipRendering.ColorFor(strokeIndex, vertexIndex);
                    return new VertexPositionColor(new Vector3(p.X, p.Y, 0), new Color(rgb.R, rgb.G, rgb.B));
                })
                .ToArray();

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineStrip, vertices, 0, vertices.Length - 1);
            }
        }

        foreach (var stroke in _lasers.Select(laser => laser.GetLine()))
        {
            if (stroke.Length < 2)
                continue;

            var vertices = stroke
                .Select(p => new VertexPositionColor(new Vector3(p.X, p.Y, 0), Color.White))
                .ToArray();

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineStrip, vertices, 0, vertices.Length - 1);
            }
        }

        base.Draw(gameTime);
    }
}
