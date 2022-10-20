namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteRenderer _spriteRenderer;
    private GraphicsPipeline _spritePipeline;
    private readonly Sampler _sampler;
    private readonly SpriteRenderer _menuRenderer;
    private float _menuDepth = 0;
    private int _numberOfTimesPressed;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
        var sw = Stopwatch.StartNew();
        _sampler = new Sampler(GraphicsDevice, SamplerCreateInfo.PointClamp);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _spritePipeline = CreateGraphicsPipeline();

        var ldtkPath = Path.Combine(ContentRoot, ContentPaths.Ldtk.MapLdtk);
        var jsonString = File.ReadAllText(ldtkPath);
        var ldtkJson = LdtkJson.FromJson(jsonString);

        var asepritePath = Path.Combine(ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
        var asepriteTexture = LoadAseprite(GraphicsDevice, asepritePath);
        _spriteRenderer = new SpriteRenderer(asepriteTexture);

        var menu = LoadPngTexture(GraphicsDevice, Path.Combine(ContentRoot, ContentPaths.Textures.MenuBackgroundPng));
        _menuRenderer = new SpriteRenderer(menu);

        Logger.LogInfo($"Game Loaded in {sw.ElapsedMilliseconds} ms");
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

    private GraphicsPipeline CreateGraphicsPipeline()
    {
        var spriteVertexShader = new ShaderModule(GraphicsDevice, "Content/Shaders/sg2_sprite.vert.spv");
        var spriteFragmentShader = new ShaderModule(GraphicsDevice, "Content/Shaders/sprite.frag.spv");

        var myVertexBindings = new VertexBinding[]
        {
            VertexBinding.Create<Position3DTextureColorVertex>()
        };

        var myVertexAttributes = new VertexAttribute[]
        {
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Position), 0),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.TexCoord), 1),
            VertexAttribute.Create<Position3DTextureColorVertex>(nameof(Position3DTextureColorVertex.Color), 2),
        };

        var myVertexInputState = new VertexInputState
        {
            VertexBindings = myVertexBindings,
            VertexAttributes = myVertexAttributes
        };

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            AttachmentInfo = new GraphicsPipelineAttachmentInfo(
                new ColorAttachmentDescription(TextureFormat.B8G8R8A8, ColorAttachmentBlendState.AlphaBlend)
            ),
            DepthStencilState = DepthStencilState.Disable,
            VertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(spriteVertexShader, "main", 0),
            FragmentShaderInfo = GraphicsShaderInfo.Create(spriteFragmentShader, "main", 1),
            MultisampleState = MultisampleState.None,
            RasterizerState = RasterizerState.CCW_CullNone,
            PrimitiveType = PrimitiveType.TriangleList,
            VertexInputState = myVertexInputState,
        };

        return new GraphicsPipeline(
            GraphicsDevice,
            myGraphicsPipelineCreateInfo
        );
    }

    protected override void Update(TimeSpan dt)
    {
        if (Inputs.Keyboard.IsPressed(KeyCode.Space))
        {
            _menuDepth = _numberOfTimesPressed % 2 == 0 ? 0.1f : -0.1f;
            _numberOfTimesPressed++;
            Logger.LogInfo($"Depth: {_menuDepth}");
        }
    }
    
    protected override void Draw(double alpha)
    {
        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        var swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);

        if (swapchainTexture == null)
            return;

        _menuRenderer.Draw(commandBuffer, _spriteBatch, Matrix3x2.Identity, Color.White, _menuDepth, _sampler);
        _spriteRenderer.Draw(commandBuffer, _spriteBatch, Matrix3x2.Identity, Color.White, 0, _sampler);

        commandBuffer.BeginRenderPass(
            new ColorAttachmentInfo(swapchainTexture, Color.CornflowerBlue)
        );

        var view = Matrix4x4.CreateLookAt(
            new Vector3(0, 0, 1),
            Vector3.Zero,
            Vector3.Up
        );

        var projection = Matrix4x4.CreateOrthographicOffCenter(
            0,
            MainWindow.Width,
            MainWindow.Height,
            0,
            0.01f,
            4000f
        );

        Matrix4x4 viewProjectionMatrix = view * projection;

        commandBuffer.BindGraphicsPipeline(_spritePipeline);

        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjectionMatrix);

        _spriteBatch.Draw(commandBuffer, vertexParamOffset);

        commandBuffer.EndRenderPass();

        GraphicsDevice.Submit(commandBuffer);
    }

    private static Texture LoadAseprite(GraphicsDevice device, string path)
    {
        var aseprite = AsepriteFile.LoadAsepriteFile(path);
        var (data, rects) = AsepriteToTextureAtlasConverter.GetTextureAtlas(aseprite);
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
