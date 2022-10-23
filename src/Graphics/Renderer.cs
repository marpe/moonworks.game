using MoonWorks.Graphics.Font;

namespace MyGame.Graphics;

public class Renderer
{
    public readonly SpriteBatch SpriteBatch;

    private TextBatcher _textBatcher;
    private readonly Sampler _sampler;
    private Texture _depthTexture;

    public ColorAttachmentBlendState FontPipelineBlend = ColorAttachmentBlendState.AlphaBlend;
    private Texture _fontTexture;
    private GraphicsPipeline _fontPipeline;
    private Font _font;
    public Packer FontPacker;
    private readonly MyGameMain _game;
    private readonly GraphicsDevice _device;
    private readonly Texture _dummyTexture;
    private int _textBatcherUploads;

    public CommandBuffer? CommandBuffer { get; private set; }
    public Texture? Swap { get; private set; }

    public Renderer(MyGameMain game)
    {
        _game = game;
        _device = game.GraphicsDevice;
        SpriteBatch = new SpriteBatch(_device);
        _sampler = new Sampler(_device, SamplerCreateInfo.PointClamp);
        _depthTexture = Texture.CreateTexture2D(_device, 1280, 720, TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        _dummyTexture = Texture.CreateTexture2D(_device, 2, 2, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);
        LoadFonts();
    }

    private void LoadFonts()
    {
        _fontPipeline = SpriteBatch.CreateGraphicsPipeline(_device, FontPipelineBlend);
        var fontPath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Fonts.RobotoRegularTtf);
        _font = new Font(fontPath);

        FontPacker = new Packer(_device, _font, 48, 512, 512);
        var fontRange = new FontRange()
        {
            FirstCodepoint = 0x20,
            NumChars = 0x7e - 0x20 + 1,
            OversampleH = 0,
            OversampleV = 0
        };
        var result = FontPacker.PackFontRanges(fontRange);

        var commandBuffer = _device.AcquireCommandBuffer();
        FontPacker.SetTextureData(commandBuffer);
        _device.Submit(commandBuffer);

        var pixels = ConvertTextureFormat(_device, FontPacker.Texture);
        var (width, height) = (FontPacker.Texture.Width, FontPacker.Texture.Height);
        _fontTexture = CreateTexture(_device, width, height, pixels);

        _textBatcher = new TextBatcher(_device);
    }

    public void RecreateFontPipeline()
    {
        _fontPipeline.Dispose();
        _fontPipeline = SpriteBatch.CreateGraphicsPipeline(_device, FontPipelineBlend);
    }

    public bool BeginFrame()
    {
        var command = _device.AcquireCommandBuffer();
        CommandBuffer = command;
        var swap = command.AcquireSwapchainTexture(_game.MainWindow);
        Swap = swap;
        if (swap == null)
        {
            Logger.LogError("Could not acquire swapchain texture");
            return false;
        }

        var windowSize = _game.MainWindow.Size;
        if (windowSize.X != _depthTexture.Width || windowSize.Y != _depthTexture.Height)
        {
            _depthTexture.Dispose();
            _depthTexture = Texture.CreateTexture2D(_device, (uint)windowSize.X, (uint)windowSize.Y,
                TextureFormat.D16, TextureUsageFlags.DepthStencilTarget);
        }

        _textBatcherUploads = 0;
        _textBatcher.Start(FontPacker);

        return true;
    }

    public void DrawSprite(Sprite sprite, Matrix3x2 transform, Color color, float depth)
    {
        var commandBuffer = CommandBuffer ?? throw new InvalidOperationException();
        SpriteBatch.AddSingle(commandBuffer, sprite, color, depth, transform);
    }

    public void DrawText(ReadOnlySpan<char> text, float x, float y, float depth, Color color,
        HorizontalAlignment alignH = HorizontalAlignment.Left, VerticalAlignment alignV = VerticalAlignment.Baseline)
    {
        _textBatcher.Draw(text.ToString(), x, y, depth, color, HorizontalAlignment.Left, VerticalAlignment.Baseline);
    }

    public void BeginRenderPass(Matrix4x4 viewProjection, bool clear = true)
    {
        var command = CommandBuffer ?? throw new InvalidOperationException();
        var swap = Swap ?? throw new InvalidOperationException();
        if (_textBatcherUploads == 0)
        {
            _textBatcher.UploadBufferData(command);
            _textBatcherUploads++;
        }

        var colorAttach = clear ? new ColorAttachmentInfo(swap, Color.CornflowerBlue) : new ColorAttachmentInfo(swap, LoadOp.Load);

        command.BeginRenderPass(
            new DepthStencilAttachmentInfo(_depthTexture, new DepthStencilValue(0, 0)),
            colorAttach
        );

        SpriteBatch.Draw(command, viewProjection);

        RenderText(command, viewProjection);
    }

    public void EndRenderPass()
    {
        var command = CommandBuffer ?? throw new InvalidOperationException();
        var swap = Swap ?? throw new InvalidOperationException();

        command.EndRenderPass();
    }

    private void RenderText(CommandBuffer command, Matrix4x4 viewProjection)
    {
        command.BindGraphicsPipeline(_fontPipeline);
        var vtxUniformOffset = command.PushVertexShaderUniforms(viewProjection);
        command.BindVertexBuffers(_textBatcher.VertexBuffer);
        command.BindIndexBuffer(_textBatcher.IndexBuffer, IndexElementSize.ThirtyTwo);
        command.BindFragmentSamplers(new TextureSamplerBinding(_fontTexture, _sampler));
        command.DrawIndexedPrimitives(0, 0, _textBatcher.PrimitiveCount, vtxUniformOffset, 0);
    }

    public void EndFrame()
    {
        var command = CommandBuffer ?? throw new InvalidOperationException();
        _device.Submit(command);
    }

    public static byte[] ConvertTextureFormat(GraphicsDevice device, Texture texture)
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

    public static unsafe Texture CreateTexture(GraphicsDevice device, uint width, uint height, byte[] pixels)
    {
        var texture = Texture.CreateTexture2D(device, width, height,
            TextureFormat.R8G8B8A8,
            TextureUsageFlags.Sampler
        );
        var cmdBuffer = device.AcquireCommandBuffer();
        fixed (byte* p = pixels)
        {
            cmdBuffer.SetTextureData(texture, (IntPtr)p, (uint)pixels.Length);
            device.Submit(cmdBuffer);
        }

        return texture;
    }
}
