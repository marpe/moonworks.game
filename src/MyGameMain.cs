using System.IO;
using ldtk;
using MyGame.Aseprite;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";

    private readonly SpriteBatch _spriteBatch;
    private readonly SpriteRenderer _spriteRenderer;
    private GraphicsPipeline _spritePipeline;
    private readonly Sampler _sampler;
    private readonly SpriteRenderer _menuRenderer;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
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

        Logger.LogInfo("Game Loaded");
    }

    private static Texture LoadPngTexture(GraphicsDevice device, string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"File not found: {path}");
        var commandBuffer = device.AcquireCommandBuffer();
        var texture = Texture.LoadPNG(device, commandBuffer, path);
        device.Submit(commandBuffer);
        Logger.LogInfo($"Loaded png texture: {path}, size: {texture.Width}, {texture.Height}");
        return texture;
    }

    private GraphicsPipeline CreateGraphicsPipeline()
    {
        var spriteVertexShader = new ShaderModule(GraphicsDevice, "Content/Shaders/sprite.vert.spv");
        var spriteFragmentShader = new ShaderModule(GraphicsDevice, "Content/Shaders/sprite.frag.spv");

        var myVertexBindings = new VertexBinding[]
        {
            VertexBinding.Create<VertexPositionTexcoord>()
        };

        var myVertexAttributes = new VertexAttribute[]
        {
            VertexAttribute.Create<VertexPositionTexcoord>(nameof(VertexPositionTexcoord.position), 0),
            VertexAttribute.Create<VertexPositionTexcoord>(nameof(VertexPositionTexcoord.texcoord), 1),
            VertexAttribute.Create<VertexPositionTexcoord>(nameof(VertexPositionTexcoord.color), 2),
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
    }

    protected override void Draw(double alpha)
    {
        var commandBuffer = GraphicsDevice.AcquireCommandBuffer();
        var swapchainTexture = commandBuffer.AcquireSwapchainTexture(MainWindow);

        if (swapchainTexture == null)
            return;

        _spriteRenderer.Draw(commandBuffer, _spriteBatch, _sampler);
        // _menuRenderer.Draw(commandBuffer, _spriteBatch, _sampler);

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
            480,
            270,
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
