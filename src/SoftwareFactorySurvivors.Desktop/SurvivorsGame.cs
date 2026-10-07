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

        var input = ShipInput.FromKeys(
            forward: keyboard.IsKeyDown(Keys.W),
            backward: keyboard.IsKeyDown(Keys.S),
            left: keyboard.IsKeyDown(Keys.A),
            right: keyboard.IsKeyDown(Keys.D));

        _ship.Update(input.Thrust, input.Rotation, (float)gameTime.ElapsedGameTime.TotalSeconds);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // Closed outline: repeat the first vertex at the end of the line strip.
        var corners = _ship.GetVertices();
        var vertices = corners
            .Append(corners[0])
            .Select(p => new VertexPositionColor(new Vector3(p.X, p.Y, 0), Color.White))
            .ToArray();

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineStrip, vertices, 0, vertices.Length - 1);
        }

        base.Draw(gameTime);
    }
}
