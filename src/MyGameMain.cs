using System.Threading.Tasks;
using MoonWorks.Graphics.Font;
using MyGame.Components;
using MyGame.Graphics;
using MyGame.TWImGui;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";

    public ulong FrameCount { get; private set; }
    public ulong RenderCount { get; private set; }
    public float TotalElapsedTime { get; private set; }

    public float ElapsedTime { get; private set; }

    private Sprite? _spriteRenderer;
    private Sprite? _menuRenderer;
    private readonly Camera _camera;
    private Vector2 _cameraRotation = new Vector2(0, MathHelper.Pi);
    private ImGuiScreen? _imGuiScreen;
    private bool _drawImGui = true;
    private KeyCode[] _modifierKeys;

    private bool _saveTexture;

    public readonly Renderer Renderer;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
        var sw = Stopwatch.StartNew();

        Renderer = new Renderer(this);

        LoadLDtk();

        LoadTextures();

        _camera = new Camera();
        _camera.Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);

        Task.Run(() => { _imGuiScreen = new ImGuiScreen(this); });

        _modifierKeys = new KeyCode[]
        {
            KeyCode.LeftControl,
            KeyCode.RightControl,
            KeyCode.LeftShift,
            KeyCode.RightShift,
            KeyCode.LeftAlt,
            KeyCode.RightAlt,
            KeyCode.LeftMeta,
            KeyCode.RightMeta,
        };

        Logger.LogInfo($"Game Loaded in {sw.ElapsedMilliseconds} ms");
    }

    private void LoadTextures()
    {
        Task.Run(() =>
        {
            var sw2 = Stopwatch.StartNew();
            var asepritePath = Path.Combine(ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
            var asepriteTexture = LoadAseprite(GraphicsDevice, asepritePath);
            _spriteRenderer = new Sprite(asepriteTexture);

            var menu = LoadPngTexture(GraphicsDevice, Path.Combine(ContentRoot, ContentPaths.Textures.MenuBackgroundPng));
            _menuRenderer = new Sprite(menu);
            Logger.LogInfo($"Loaded textures in {sw2.ElapsedMilliseconds} ms");
        });
    }

    private void LoadLDtk()
    {
        Task.Run(() =>
        {
            var sw2 = Stopwatch.StartNew();
            var ldtkPath = Path.Combine(ContentRoot, ContentPaths.Ldtk.MapLdtk);
            var jsonString = File.ReadAllText(ldtkPath);
            var ldtkJson = LdtkJson.FromJson(jsonString);
            Logger.LogInfo($"Loaded LDtk in {sw2.ElapsedMilliseconds} ms");
        });
    }

    private static Texture LoadPngTexture(GraphicsDevice device, string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"File not found: {path}");
        var commandBuffer = device.AcquireCommandBuffer();
        var texture = Texture.LoadPNG(device, commandBuffer, path);
        device.Submit(commandBuffer);
        return texture;
    }

    private bool IsAnyModifierKeyDown()
    {
        foreach (var key in _modifierKeys)
        {
            if (Inputs.Keyboard.IsDown(key))
                return true;
        }

        return false;
    }

    protected override void Update(TimeSpan dt)
    {
        FrameCount++;
        ElapsedTime = (float)dt.TotalSeconds;
        TotalElapsedTime += ElapsedTime;

        if (_imGuiScreen != null)
            _imGuiScreen.Update();

        if (IsAnyModifierKeyDown())
            return;

        if (Inputs.Keyboard.IsPressed(KeyCode.P))
        {
            _saveTexture = true;
        }

        if (Inputs.Keyboard.IsPressed(KeyCode.F1))
        {
            _camera.Use3D = !_camera.Use3D;
        }

        if (Inputs.Keyboard.IsPressed(KeyCode.F2))
        {
            _drawImGui = !_drawImGui;
        }

        /*if (Inputs.Keyboard.IsPressed(KeyCode.F3))
        {
            var numBlendStates = Enum.GetValues<BlendState>().Length;
            var nextMode = (numBlendStates + (int)SpriteBatch.BlendState + 1) % numBlendStates;
            SpriteBatch.BlendState = (BlendState)nextMode;
            Logger.LogInfo($"BlendState: {SpriteBatch.BlendState}");
            // MainWindow.ChangeScreenMode((ScreenMode)nextMode);
        }

        if (Inputs.Keyboard.IsPressed(KeyCode.F4))
        {
            var numBlendStates = Enum.GetValues<BlendState>().Length;
            var nextMode = (numBlendStates + (int)SpriteBatch.BlendState - 1) % numBlendStates;
            SpriteBatch.BlendState = (BlendState)nextMode;
            Logger.LogInfo($"BlendState: {SpriteBatch.BlendState}");
            // MainWindow.ChangeScreenMode((ScreenMode)nextMode);
        }*/

        if (_camera.Use3D)
        {
            if (Inputs.Keyboard.IsPressed(KeyCode.Home))
            {
                _cameraRotation = new Vector2(0, MathHelper.Pi);
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                _camera.Rotation3D = rotation;
                _camera.Position3D = new Vector3(0, 0, -1000);
            }

            if (Inputs.Mouse.RightButton.IsHeld)
            {
                var rotationSpeed = 0.1f;
                _cameraRotation += new Vector2(Inputs.Mouse.DeltaX, -Inputs.Mouse.DeltaY) * rotationSpeed * (float)dt.TotalSeconds;
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                _camera.Rotation3D = rotation;
            }

            var cameraSpeed = 750f;
            if (Inputs.Keyboard.IsDown(KeyCode.W))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Forward, _camera.Rotation3D) * cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.S))
            {
                _camera.Position3D -= Vector3.Transform(Vector3.Forward, _camera.Rotation3D) * cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.A))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Left, _camera.Rotation3D) * cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.D))
            {
                _camera.Position3D += Vector3.Transform(Vector3.Right, _camera.Rotation3D) * cameraSpeed * (float)dt.TotalSeconds;
            }
        }
        else
        {
            var cameraSpeed = 500f;
            if (Inputs.Keyboard.IsDown(KeyCode.W))
            {
                _camera.Position.Y -= cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.S))
            {
                _camera.Position.Y += cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.A))
            {
                _camera.Position.X -= cameraSpeed * (float)dt.TotalSeconds;
            }

            if (Inputs.Keyboard.IsDown(KeyCode.D))
            {
                _camera.Position.X += cameraSpeed * (float)dt.TotalSeconds;
            }
        }
    }


    protected override void Draw(double alpha)
    {
        if (MainWindow.IsMinimized)
            return;

        RenderCount++;

        if (!Renderer.BeginFrame())
            return;

        var (cb, sp) = (
            Renderer.CommandBuffer ?? throw new InvalidOperationException(),
            Renderer.Swap ?? throw new InvalidOperationException()
        );

        if (_menuRenderer != null)
            Renderer.DrawSprite(_menuRenderer.Value, Matrix3x2.CreateScale(3f, 3f) * Matrix3x2.CreateTranslation(-200, -100), Color.White,
                200f);

        Renderer.DrawText("Hello hej!", 0, 0, 0, Color.White);
        Renderer.DrawText("Oooga chacakka!", 100, 100, 0, Color.White);

        // _spriteRenderer?.Draw(commandBuffer, SpriteBatch, Matrix3x2.Identity, Color.White, 0);

        _camera.Size = MainWindow.Size;
        Renderer.BeginRenderPass(_camera.ViewProjectionMatrix);
        // stuff
        Renderer.EndRenderPass();

        if (_imGuiScreen != null && _drawImGui)
        {
            _imGuiScreen.Draw(Renderer);
        }

        Renderer.EndFrame();

        if (_saveTexture)
        {
            // SaveTextureToPng(GraphicsDevice, _fontTexture, "fontTexture.png");
            SaveTextureToPng(GraphicsDevice, Renderer.FontPacker.Texture, "fontPacker.png");
            _saveTexture = false;
        }
    }

    private static void SaveTextureToPng(GraphicsDevice device, Texture texture, string path)
    {
        var pixels = Renderer.ConvertTextureFormat(device, texture);
        Texture.SavePNG(path, (int)texture.Width, (int)texture.Height, TextureFormat.R8G8B8A8, pixels);
    }

    private static Texture LoadAseprite(GraphicsDevice device, string path)
    {
        var aseprite = AsepriteFile.LoadAsepriteFile(path);
        var (data, rects) = AsepriteToTextureAtlasConverter.GetTextureData(aseprite);
        var texture = Texture.CreateTexture2D(
            device,
            aseprite.Header.Width * (uint)aseprite.Frames.Count,
            aseprite.Header.Height,
            TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler
        );
        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetTextureData(texture, data);
        device.Submit(commandBuffer);
        return texture;
    }

    protected override void Destroy()
    {
    }
}
