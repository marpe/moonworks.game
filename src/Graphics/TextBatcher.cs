using MoonWorks.Graphics.Font;

namespace MyGame.Graphics;

public enum TextFont
{
    Roboto,
    ConsolasMono
}

public class FontData
{
    public Packer Packer;
    public Font Font;
    public Texture Texture;
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

    private TextBatch _textBatch;
    private uint _addCountSinceDraw = 0;
    public uint AddCountSinceDraw => _addCountSinceDraw;

    private readonly Dictionary<TextFont, FontData> _fonts = new();
    private FontData? _currentFont = null;

    public TextBatcher(GraphicsDevice device)
    {
        _textBatch = new TextBatch(device);

        var fonts = new[]
        {
            (TextFont.Roboto, ContentPaths.Fonts.RobotoRegularTtf),
            (TextFont.ConsolasMono, ContentPaths.Fonts.ConsolaTtf)
        };

        var commandBuffer = device.AcquireCommandBuffer();
        foreach (var (key, path) in fonts)
        {
            var fontPath = Path.Combine(MyGameMain.ContentRoot, path);
            var font = new Font(fontPath);
            var fontPacker = new Packer(device, font, 18f, 512, 512);
            fontPacker.PackFontRanges(FontRange);
            fontPacker.SetTextureData(commandBuffer);
            var textBatchFont = new FontData()
            {
                Font = font,
                Packer = fontPacker,
            };
            _fonts.Add(key, textBatchFont);
        }

        device.Submit(commandBuffer);

        foreach (var (key, data) in _fonts)
        {
            var pixels = Renderer.ConvertTextureFormat(device, data.Packer.Texture);
            var (width, height) = (data.Packer.Texture.Width, data.Packer.Texture.Height);
            var fontTexture = Renderer.CreateTexture(device, width, height, pixels);
            _fonts[key].Texture = fontTexture;
        }
    }

    public void Start(TextFont fontType = TextFont.Roboto)
    {
        if (_currentFont == null || _currentFont != _fonts[fontType])
            _currentFont = _fonts[fontType]; // TODO (marpe): start a subbatch
        _textBatch.Start(_currentFont.Packer);
    }

    public void Add(ReadOnlySpan<char> text, float x, float y, float depth, Color color, HorizontalAlignment alignH,
        VerticalAlignment alignV)
    {
        _addCountSinceDraw++;
        _textBatch.Draw(text, x, y, depth, color, alignH, alignV);
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
        var texture = _currentFont?.Texture ?? throw new InvalidOperationException();
        command.BindFragmentSamplers(new TextureSamplerBinding(texture, Renderer.PointClamp));
        command.DrawIndexedPrimitives(0, 0, _textBatch.PrimitiveCount, vtxUniformOffset, 0);
        _addCountSinceDraw = 0;
    }
}
