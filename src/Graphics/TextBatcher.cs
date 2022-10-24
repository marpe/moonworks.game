using MoonWorks.Graphics.Font;

namespace MyGame.Graphics;

public class TextBatcher
{
    private TextBatch _textBatch;
    private uint _addCountSinceDraw = 0;
    public uint AddCountSinceDraw => _addCountSinceDraw;

    private Texture _fontTexture;
    private Font _font;
    private readonly Packer _fontPacker;

    public TextBatcher(GraphicsDevice device)
    {
        _textBatch = new TextBatch(device);

        var fontPath = Path.Combine(MyGameMain.ContentRoot, ContentPaths.Fonts.RobotoRegularTtf);
        _font = new Font(fontPath);

        _fontPacker = new Packer(device, _font, 48, 512, 512);
        var fontRange = new FontRange()
        {
            FirstCodepoint = 0x20,
            NumChars = 0x7e - 0x20 + 1,
            OversampleH = 0,
            OversampleV = 0
        };
        var result = _fontPacker.PackFontRanges(fontRange);

        var commandBuffer = device.AcquireCommandBuffer();
        _fontPacker.SetTextureData(commandBuffer);
        device.Submit(commandBuffer);

        var pixels = Renderer.ConvertTextureFormat(device, _fontPacker.Texture);
        var (width, height) = (_fontPacker.Texture.Width, _fontPacker.Texture.Height);
        _fontTexture = Renderer.CreateTexture(device, width, height, pixels);
    }

    public void Start()
    {
        _textBatch.Start(_fontPacker);
    }

    public void Add(ReadOnlySpan<char> text, float x, float y, float depth, Color color, HorizontalAlignment alignH,
        VerticalAlignment alignV)
    {
        _addCountSinceDraw++;
        _textBatch.Draw(text.ToString(), x, y, depth, color, alignH, alignV);
    }

    public void PushVertexData(CommandBuffer command)
    {
        _textBatch.UploadBufferData(command);
    }

    public void Draw(CommandBuffer command, Matrix4x4 viewProjection)
    {
        var vtxUniformOffset = command.PushVertexShaderUniforms(viewProjection);
        command.BindVertexBuffers(_textBatch.VertexBuffer);
        command.BindIndexBuffer(_textBatch.IndexBuffer, IndexElementSize.ThirtyTwo);
        var texture = _fontTexture;
        command.BindFragmentSamplers(new TextureSamplerBinding(texture, Renderer.PointClamp));
        command.DrawIndexedPrimitives(0, 0, _textBatch.PrimitiveCount, vtxUniformOffset, 0);
        _addCountSinceDraw = 0;
    }
}
