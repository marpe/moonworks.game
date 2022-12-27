namespace MyGame.Graphics;

public class TextBatcher
{
    private readonly Dictionary<FontType, FontData> _fonts = new();

    private uint _addCountSinceDraw;

    public static readonly FontRange BasicLatin = new()
    {
        FirstCodepoint = 0x0,
        NumChars = 0x7f + 1,
        OversampleH = 0,
        OversampleV = 0,
    };

    public TextBatcher()
    {
        var pixellari = Shared.Content.Load<TTFFont>(ContentPaths.fonts.Pixellari_ttf);
        var roboto = Shared.Content.Load<TTFFont>(ContentPaths.fonts.Roboto_Regular_ttf);
        var consola = Shared.Content.Load<TTFFont>(ContentPaths.fonts.consola_ttf);

        _fonts.Add(FontType.Pixellari, pixellari.Sizes[18]);
        _fonts.Add(FontType.PixellariLarge, pixellari.Sizes[48]);

        _fonts.Add(FontType.RobotoMedium, roboto.Sizes[18]);
        _fonts.Add(FontType.RobotoLarge, roboto.Sizes[48]);

        _fonts.Add(FontType.ConsolasMonoMedium, consola.Sizes[18]);
        _fonts.Add(FontType.ConsolasMonoLarge, consola.Sizes[48]);
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
                sprite.TextureSlice = font.Texture ?? throw new InvalidOperationException();
                var fontTextureSize = new Vector2(font.Texture.Width, font.Texture.Height);

                for (var i = 0; i < numVerts; i += 4)
                {
                    var topLeftVert = vertices[i];
                    var bottomRightVert = vertices[i + 3];
                    var transform = Matrix3x2.CreateTranslation(new Vector2((int)topLeftVert.Position.X, (int)topLeftVert.Position.Y));
                    var srcPos = topLeftVert.TexCoord * fontTextureSize;
                    var srcDim = (bottomRightVert.TexCoord - topLeftVert.TexCoord) * fontTextureSize;
                    var srcRect = new Rectangle((int)srcPos.X, (int)srcPos.Y, (int)srcDim.X, (int)srcDim.Y);

                    sprite.SrcRect = srcRect;
                    Sprite.GenerateUVs(ref sprite.UV, sprite.TextureSlice, srcRect);
                    var color = topLeftVert.Color;
                    spriteBatch.Draw(sprite, color, topLeftVert.Position.Z, transform.ToMatrix4x4(), Renderer.PointClamp);
                }
            }

            font.HasStarted = false;
        }

        _addCountSinceDraw = 0;
    }
}
