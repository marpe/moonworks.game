using System.Threading.Tasks;
using MoonWorks.Graphics.Font;
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

    public readonly SpriteBatch SpriteBatch;
    private SpriteRenderer? _spriteRenderer;
    private SpriteRenderer? _menuRenderer;
    private readonly Camera _camera;
    private Texture _depthTexture;
    private Vector2 _cameraRotation = new Vector2(0, MathHelper.Pi);
    private ImGuiScreen? _imGuiScreen;
    private bool _drawImGui = true;
    private KeyCode[] _modifierKeys;
    private Font _font;
    private Packer _fontPacker;
    private bool _saveTexture;
    private TextBatch _textBatch;
    private readonly Sampler _sampler;
    private GraphicsPipeline _fontPipeline;
    public ColorAttachmentBlendState FontPipelineBlend = ColorAttachmentBlendState.AlphaBlend;
    private Texture _fontTexture;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
        var sw = Stopwatch.StartNew();
        SpriteBatch = new SpriteBatch(GraphicsDevice);

        LoadLDtk();

        LoadTextures();

        LoadFonts();

        _camera = new Camera();
        _camera.Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);

        _sampler = new Sampler(GraphicsDevice, SamplerCreateInfo.PointClamp);
        _depthTexture = Texture.CreateTexture2D(GraphicsDevice, 1280, 720, TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);

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

    private unsafe Texture CreateTexture(GraphicsDevice device, uint width, uint height, byte[] pixels)
    {
        var texture = Texture.CreateTexture2D(GraphicsDevice, width, height,
            TextureFormat.R8G8B8A8,
            TextureUsageFlags.Sampler
        );
        var cmdBuffer = GraphicsDevice.AcquireCommandBuffer();
        fixed (byte* p = pixels)
        {
            cmdBuffer.SetTextureData(texture, (IntPtr)p, (uint)pixels.Length);
            GraphicsDevice.Submit(cmdBuffer);
        }

        return texture;
    }

    private void LoadFonts()
    {
        _fontPipeline = SpriteBatch.CreateGraphicsPipeline(GraphicsDevice, FontPipelineBlend);
        var fontPath = Path.Combine(ContentRoot, ContentPaths.Fonts.RobotoRegularTtf);
        _font = new Font(fontPath);

        _fontPacker = new Packer(GraphicsDevice, _font, 48, 512, 512);
        var fontRange = new FontRange()
        {
            FirstCodepoint = 0x20,
            NumChars = 0x7e - 0x20 + 1,
            OversampleH = 0,
            OversampleV = 0
        };
        var result = _fontPacker.PackFontRanges(fontRange);

        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        _fontPacker.SetTextureData(commandBuffer);
        GraphicsDevice.Submit(commandBuffer);

        var pixels = ConvertTextureFormat(GraphicsDevice, _fontPacker.Texture);
        var (width, height) = (_fontPacker.Texture.Width, _fontPacker.Texture.Height);
        _fontTexture = CreateTexture(GraphicsDevice, width, height, pixels);

        _textBatch = new TextBatch(GraphicsDevice);
    }

    private void LoadTextures()
    {
        Task.Run(() =>
        {
            var sw2 = Stopwatch.StartNew();
            var asepritePath = Path.Combine(ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
            var asepriteTexture = LoadAseprite(GraphicsDevice, asepritePath);
            _spriteRenderer = new SpriteRenderer(asepriteTexture);

            var menu = LoadPngTexture(GraphicsDevice, Path.Combine(ContentRoot, ContentPaths.Textures.MenuBackgroundPng));
            _menuRenderer = new SpriteRenderer(menu);
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

    public void RecreateFontPipeline()
    {
        _fontPipeline.Dispose();
        _fontPipeline = SpriteBatch.CreateGraphicsPipeline(GraphicsDevice, FontPipelineBlend);
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

        if (Inputs.Keyboard.IsPressed(KeyCode.F3))
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
        }

        if (_camera.Use3D)
        {
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
        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        var swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);

        if (swapchainTexture == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return;
        }

        var windowSize = MainWindow.Size;
        if (windowSize.X != _depthTexture.Width || windowSize.Y != _depthTexture.Height)
        {
            _depthTexture.Dispose();
            _depthTexture = Texture.CreateTexture2D(GraphicsDevice, (uint)windowSize.X, (uint)windowSize.Y,
                TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        }

        _menuRenderer?.Draw(commandBuffer, SpriteBatch, Matrix3x2.CreateScale(3f, 3f) * Matrix3x2.CreateTranslation(-200, -100), Color.White, 200f);
        // _spriteRenderer?.Draw(commandBuffer, SpriteBatch, Matrix3x2.Identity, Color.White, 0);

        _textBatch.Start(_fontPacker);
        _textBatch.Draw("Text", 0, 0, 0, Color.White);
        _textBatch.Draw("Rendering With", 100, 100, 0, Color.White);
        _textBatch.Draw("Wellspring ", 300, 300, 0, Color.White);
        _textBatch.UploadBufferData(commandBuffer);

        commandBuffer.BeginRenderPass(
            new DepthStencilAttachmentInfo(_depthTexture, new DepthStencilValue(0, 0)),
            new ColorAttachmentInfo(swapchainTexture, Color.CornflowerBlue)
        );
        _camera.Size = windowSize;
        SpriteBatch.Draw(commandBuffer, _camera.ViewProjectionMatrix);

        // render text
        {
            commandBuffer.BindGraphicsPipeline(_fontPipeline);
            var vtxUniformOffset = commandBuffer.PushVertexShaderUniforms(_camera.ViewProjectionMatrix);
            commandBuffer.BindVertexBuffers(_textBatch.VertexBuffer);
            commandBuffer.BindIndexBuffer(_textBatch.IndexBuffer, IndexElementSize.ThirtyTwo);
            commandBuffer.BindFragmentSamplers(new TextureSamplerBinding(_fontTexture, _sampler));
            commandBuffer.DrawIndexedPrimitives(0, 0, _textBatch.PrimitiveCount, vtxUniformOffset, 0);
        }

        commandBuffer.EndRenderPass();

        if (_imGuiScreen != null && _drawImGui)
        {
            _imGuiScreen.Draw(_depthTexture, commandBuffer, swapchainTexture);
        }

        GraphicsDevice.Submit(commandBuffer);

        if (_saveTexture)
        {
            SaveTextureToPng(GraphicsDevice, _fontTexture, "fontTexture.png");
            SaveTextureToPng(GraphicsDevice, _fontPacker.Texture, "fontPacker.png");
            _saveTexture = false;
        }
    }

    private static byte[] ConvertTextureFormat(GraphicsDevice device, Texture texture)
    {
        var pixelSize = texture.Format switch
        {
            TextureFormat.R8 => 8u,
            _ => 32u,
        };
        var buffer = MoonWorks.Graphics.Buffer.Create<byte>(device, BufferUsageFlags.Index, texture.Width * texture.Height * pixelSize);
        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.CopyTextureToBuffer(texture, buffer);
        device.Submit(commandBuffer);
        device.Wait();
        var pixels = new byte[buffer.Size];
        buffer.GetData(pixels, (uint)pixels.Length);
        if (texture.Format == TextureFormat.R8)
        {
            var prevLength = pixels.Length;
            Array.Resize(ref pixels, pixels.Length * 4);
            for (var i = prevLength - 1; i >= 0; i--)
            {
                var p = pixels[i];
                pixels[i] = 0;
                pixels[i * 4] = 255;
                pixels[i * 4 + 1] = 255;
                pixels[i * 4 + 2] = 255;
                pixels[i * 4 + 3] = p;
            }
        }

        return pixels;
    }

    private static void SaveTextureToPng(GraphicsDevice device, Texture texture, string path)
    {
        var pixels = ConvertTextureFormat(device, texture);
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
