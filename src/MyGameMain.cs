using System.IO;
using ldtk;
using MyGame.Aseprite;

namespace MyGame;

public class MyGameMain : Game
{
    public const string ContentRoot = "Content";

    private readonly SpriteBatch _spriteBatch;
    private readonly Sprite _sprite;
    private readonly SpriteRenderer _spriteRenderer;
    private readonly GraphicsPipeline _spritePipeline;

    public MyGameMain(
        WindowCreateInfo windowCreateInfo,
        FrameLimiterSettings frameLimiterSettings,
        bool debugMode
    ) : base(windowCreateInfo, frameLimiterSettings, 60, debugMode)
    {
        var ldtkPath = Path.Combine(ContentRoot, ContentPaths.Ldtk.MapLdtk);
        var jsonString = File.ReadAllText(ldtkPath);
        var ldtkJson = LdtkJson.FromJson(jsonString);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        var asepritePath = Path.Combine(ContentRoot, ContentPaths.Ldtk.Tileset1Aseprite);
        var asepriteTexture = LoadAseprite(GraphicsDevice, asepritePath);
        var texturePage = new TexturePage()
        {
            Texture = asepriteTexture,
            Width = (int)asepriteTexture.Width,
            Height = (int)asepriteTexture.Height,
        };
        var sliceRect = new Rect();
        var frameRect = sliceRect;
        _sprite = new Sprite(texturePage, sliceRect, frameRect);
        _spriteRenderer = new SpriteRenderer(GraphicsDevice, _sprite);
        Logger.LogInfo("Game Loaded");

        var spriteVertexShader = new ShaderModule(GraphicsDevice, "sg2_sprite_vert.spv");

        var vertexShaderInfo = GraphicsShaderInfo.Create<Matrix4x4>(spriteVertexShader, "main", 0);

        var myGraphicsPipelineCreateInfo = new GraphicsPipelineCreateInfo
        {
            // AttachmentInfo = myAttachmentInfo,
            // DepthStencilState = myDepthStencilState,
            VertexShaderInfo = vertexShaderInfo,
            // FragmentShaderInfo = myFragmentShaderInfo,
            // MultisampleState = myMultisampleState,
            // RasterizerState = myRasterizerState,
            PrimitiveType = PrimitiveType.TriangleList,
            // VertexInputState = myVertexInputState,
            // ViewportState = myViewportState,
            // BlendConstants = myBlendConstants
        };

        _spritePipeline = new GraphicsPipeline(
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

        _spriteRenderer.Draw(commandBuffer, _spriteBatch);

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

        var viewProjectionMatrix = view * projection;

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
