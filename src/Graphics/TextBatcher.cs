using MoonWorks.Graphics.Font;

namespace MyGame.Graphics;

public enum FontType
{
    Roboto,
    ConsolasMono
}

public class FontData
{
    public TextBatch Batch;
    public Packer Packer;
    public Font Font;
    public Texture Texture;
    public FontType Name;
    public TextureSamplerBinding Binding;
    public bool HasStarted;
}

public class TextBatcher
{
    public FontRange FontRange = new()
    {
        FirstCodepoint = 0x20,
        NumChars = 0x7e - 0x20 + 1,
        OversampleH = 0,
        OversampleV = 0
    };

    private uint _addCountSinceDraw = 0;
    public uint AddCountSinceDraw => _addCountSinceDraw;

    private readonly Dictionary<FontType, FontData> _fonts = new();

    public TextBatcher(GraphicsDevice device)
    {
        var fonts = new[]
        {
            (FontType.Roboto, ContentPaths.Fonts.RobotoRegularTtf),
            (FontType.ConsolasMono, ContentPaths.Fonts.ConsolaTtf)
        };

        var commandBuffer = device.AcquireCommandBuffer();
        foreach (var (key, path) in fonts)
        {
            var fontPath = Path.Combine(MyGameMain.ContentRoot, path);
            var font = new Font(fontPath);
            var fontPacker = new Packer(device, font, 18f, 512, 512, 2u);
            fontPacker.PackFontRanges(FontRange);
            fontPacker.SetTextureData(commandBuffer);
            var textBatchFont = new FontData()
            {
                Font = font,
                Packer = fontPacker,
                Name = key,
                Batch = new TextBatch(device),
                HasStarted = false,
            };
            _fonts.Add(key, textBatchFont);
        }

        device.Submit(commandBuffer);

        foreach (var (key, data) in _fonts)
        {
            var pixels = TextureUtils.ConvertSingleChannelTextureToRGBA(device, data.Packer.Texture);
            var (width, height) = (data.Packer.Texture.Width, data.Packer.Texture.Height);
            var fontTexture = TextureUtils.CreateTexture(device, width, height, pixels);
            _fonts[key].Texture = fontTexture;
            _fonts[key].Binding = new TextureSamplerBinding(fontTexture, Renderer.PointClamp);
        }
    }

    public void Add(FontType fontTypeType, ReadOnlySpan<char> text, float x, float y, float depth, Color color, HorizontalAlignment alignH,
        VerticalAlignment alignV)
    {
        _addCountSinceDraw++;

        var font = _fonts[fontTypeType];
        if (!font.HasStarted)
        {
            font.Batch.Start(font.Packer);
            font.HasStarted = true;
        }

        font.Batch.Draw(text, x, y, depth, color, alignH, alignV);
    }

    public void Flush(CommandBuffer commandBuffer, GraphicsPipeline pipeline, Matrix4x4 viewProjection, DepthStencilAttachmentInfo depthStencilAttachmentInfo, ColorAttachmentInfo colorAttachmentInfo)
    {
        if (AddCountSinceDraw == 0)
            return;

        foreach (var (key, font) in _fonts)
        {
            if (font.HasStarted)
                font.Batch.UploadBufferData(commandBuffer);
        }
        
        commandBuffer.BeginRenderPass(depthStencilAttachmentInfo, new ColorAttachmentInfo(colorAttachmentInfo.Texture, LoadOp.Load));

        commandBuffer.BindGraphicsPipeline(pipeline);
        
        var vertexParamOffset = commandBuffer.PushVertexShaderUniforms(viewProjection);
        var fragmentParamOffset = 0u;

        foreach (var (key, font) in _fonts)
        {
            if (!font.HasStarted)
                continue;

            commandBuffer.BindVertexBuffers(font.Batch.VertexBuffer);
            commandBuffer.BindIndexBuffer(font.Batch.IndexBuffer, IndexElementSize.ThirtyTwo);
            commandBuffer.BindFragmentSamplers(font.Binding);
            commandBuffer.DrawIndexedPrimitives(
                0,
                0,
                font.Batch.PrimitiveCount,
                vertexParamOffset,
                fragmentParamOffset
            );
            
            font.HasStarted = false;
        }
        
        commandBuffer.EndRenderPass();

        _addCountSinceDraw = 0;
    }
}
