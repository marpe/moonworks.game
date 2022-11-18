namespace MyGame.Graphics;

public class TextBatcher
{
    private readonly Dictionary<FontType, FontData> _fonts = new();

    private uint _addCountSinceDraw = 0;
    private GraphicsDevice _device;

    public FontRange BasicLatin = new()
    {
        FirstCodepoint = 0x0,
        NumChars = 0x7f + 1,
        OversampleH = 0,
        OversampleV = 0,
    };

    public TextBatcher(GraphicsDevice device)
    {
        _device = device;

        var fonts = new[]
        {
            (FontType.RobotoMedium, 18f, ContentPaths.fonts.Roboto_Regular_ttf),
            (FontType.RobotoLarge, 48f, ContentPaths.fonts.Roboto_Regular_ttf),
            (FontType.ConsolasMonoMedium, 18f, ContentPaths.fonts.consola_ttf),
            (FontType.ConsolasMonoLarge, 48f, ContentPaths.fonts.consola_ttf),
        };

        var commandBuffer = device.AcquireCommandBuffer();
        foreach (var (key, size, path) in fonts)
        {
            var font = new Font(path);
            var fontPacker = new Packer(device, font, size, 512, 512, 2u);
            fontPacker.PackFontRanges(BasicLatin);
            fontPacker.SetTextureData(commandBuffer);
            var textBatchFont = new FontData(key, new TextBatch(device), fontPacker, font);
            _fonts.Add(key, textBatchFont);
        }

        device.Submit(commandBuffer);

        foreach (var (key, data) in _fonts)
        {
            var pixels = TextureUtils.ConvertSingleChannelTextureToRGBA(device, data.Packer.Texture);
            TextureUtils.PremultiplyAlpha(pixels);
            var (width, height) = (data.Packer.Texture.Width, data.Packer.Texture.Height);
            var fontTexture = TextureUtils.CreateTexture(device, width, height, pixels);
            _fonts[key].Texture = fontTexture;
        }
    }

    public FontData GetFont(FontType fontType)
    {
        return _fonts[fontType];
    }

    public void Unload()
    {
        foreach (var (key, fontData) in _fonts)
        {
            fontData.Batch.Dispose();
            fontData.Packer.Dispose();
            fontData.Font.Dispose();
            fontData.Texture?.Dispose();
        }

        _fonts.Clear();
    }

    public void Add(FontType fontType, ReadOnlySpan<char> text, float x, float y, float depth, Color color, HorizontalAlignment alignH,
        VerticalAlignment alignV)
    {
        if (text.Length == 0)
        {
            return;
        }

        _addCountSinceDraw++;

        var font = _fonts[fontType];
        if (!font.HasStarted)
        {
            font.Batch.Start(font.Packer);
            font.HasStarted = true;
        }

        font.Batch.Draw(text, x, y, depth, color, alignH, alignV);
    }

    public void FlushToSpriteBatch(SpriteBatch spriteBatch)
    {
        if (_addCountSinceDraw == 0)
        {
            return;
        }

        foreach (var (key, font) in _fonts)
        {
            if (!font.HasStarted)
            {
                continue;
            }

            Wellspring.Wellspring_GetBufferData(
                font.Batch.Handle,
                out var vertexCount,
                out var vertexDataPointer,
                out var vertexDataLengthInBytes,
                out var indexDataPointer,
                out var indexDataLengthInBytes
            );

            unsafe
            {
                var vertices = (Vertex*)vertexDataPointer.ToPointer();
                var sizeOfVert = Marshal.SizeOf<Vertex>();
                var numVerts = vertexDataLengthInBytes / sizeOfVert;

                var sprite = new Sprite();
                sprite.Texture = font.Texture ?? throw new InvalidOperationException();
                var fontTextureSize = new Vector2(font.Texture.Width, font.Texture.Height);

                for (var i = 0; i < numVerts; i += 4)
                {
                    var topLeftVert = vertices[i];
                    var bottomRightVert = vertices[i + 3];
                    var transform = Matrix3x2.CreateTranslation(new Vector2(topLeftVert.Position.X, topLeftVert.Position.Y));
                    var srcPos = topLeftVert.TexCoord * fontTextureSize;
                    var srcDim = (bottomRightVert.TexCoord - topLeftVert.TexCoord) * fontTextureSize;
                    var srcRect = new Rectangle((int)srcPos.X, (int)srcPos.Y, (int)srcDim.X, (int)srcDim.Y);
                    sprite.SrcRect = srcRect;
                    Sprite.GenerateUVs(ref sprite.UV, sprite.Texture, sprite.SrcRect);
                    var color = topLeftVert.Color;
                    spriteBatch.Draw(sprite, color, topLeftVert.Position.Z, transform.ToMatrix4x4(), Renderer.PointClamp);
                }
            }

            font.HasStarted = false;
        }

        _addCountSinceDraw = 0;
    }
}
