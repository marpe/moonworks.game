using MyGame.BitmapFonts;

namespace MyGame.Graphics;

public class BMFont : IDisposable
{
    private static Sprite _tempSprite = new();
    private static Kerning _tempKerning = new();
    private static Matrix3x2 _tempMatrix = Matrix3x2.Identity;

    public bool IsDisposed { get; private set; }

    public BitmapFont Font;

    public Texture[] Textures;

    public BMFont(GraphicsDevice device, string filename)
    {
        using var stream = File.OpenRead(filename); // TitleContainer.OpenStream(filename);
        using var reader = new StreamReader(stream);
        Font = BitmapFont.LoadXml(reader);
        var directoryName = Path.GetDirectoryName(filename);
        Textures = new Texture[Font.Pages.Length];

        var commandBuffer = device.AcquireCommandBuffer();
        for (var i = 0; i < Textures.Length; i++)
        {
            var path = Path.Combine(directoryName ?? "", Font.Pages[i].Filename);
            var texture = TextureUtils.LoadPngTexture(device, path);
            Textures[i] = TextureUtils.PremultiplyAlpha(device, texture);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (var tex in Textures)
            {
                tex.Dispose();
            }
        }

        IsDisposed = true;
    }

    public static Vector2 DrawInto(Renderer renderer, BMFont bmFont, ReadOnlySpan<char> text, Vector2 position, Color color, float rotation,
        Vector2 origin, Vector2 scale, float depth)
    {
        var font = bmFont.Font;

        /*var o = Matrix3x2.CreateTranslation(-origin.X, -origin.Y);
        var s = Matrix3x2.CreateScale(scale.X, scale.Y);
        var r = Matrix3x2.CreateRotation(rotation);
        var t = Matrix3x2.CreateTranslation(position.X, position.Y);
        var transformationMatrix = o * s * r * t; // o * t * r * s;*/

        var previousCharacter = ' ';
        Character? currentChar = null;
        var offset = Vector2.Zero;

        for (var i = 0; i < text.Length; ++i)
        {
            var c = text[i];

            if (c == '\r')
                continue;

            if (c == '\n')
            {
                offset.X = 0;
                offset.Y += font.LineHeight;
                currentChar = null;
                continue;
            }

            if (currentChar != null)
                offset.X += font.Spacing.X + currentChar.XAdvance;

            currentChar = font.Characters.ContainsKey(c) ? font.Characters[c] : font.DefaultCharacter;

            SetTranslationMatrix(
                ref _tempMatrix,
                position.X + offset.X + currentChar.Offset.X + bmFont.GetKerning(previousCharacter, currentChar.Char),
                position.Y + offset.Y + currentChar.Offset.Y
            );

            _tempSprite.Texture = bmFont.Textures[currentChar.TexturePage];
            _tempSprite.SrcRect = currentChar.Bounds;
            Sprite.GenerateUVs(ref _tempSprite.UV, _tempSprite.Texture, _tempSprite.SrcRect);

            renderer.DrawSprite(_tempSprite, _tempMatrix /** transformationMatrix*/, color, depth);

            previousCharacter = c;
        }

        if (currentChar != null)
        {
            offset.X += font.Spacing.X + currentChar.XAdvance;
        }

        return offset;
    }

    private static void SetTranslationMatrix(ref Matrix3x2 mat, float xPosition, float yPosition)
    {
        mat.M11 = 1.0f;
        mat.M12 = 0.0f;
        mat.M21 = 0.0f;
        mat.M22 = 1.0f;

        mat.M31 = xPosition;
        mat.M32 = yPosition;
    }

    private int GetKerning(char previous, char current)
    {
        _tempKerning.FirstCharacter = previous;
        _tempKerning.SecondCharacter = current;
        _tempKerning.Amount = 0;
        if (!Font.Kernings.TryGetValue(_tempKerning, out var result))
            return 0;
        return result;
    }

    public Vector2 MeasureString(ReadOnlySpan<char> text, float maxWidth = float.MaxValue)
    {
        if (text.Length == 0)
        {
            return Vector2.Zero;
        }

        var length = text.Length;
        var previousCharacter = ' ';
        var currentLineWidth = 0;
        var currentLineHeight = Font.LineHeight;
        var blockWidth = 0;
        var blockHeight = 0;

        void Linefeed()
        {
            blockHeight += Font.LineHeight;
            blockWidth = Math.Max(blockWidth, currentLineWidth);
            currentLineWidth = 0;
            currentLineHeight = Font.LineHeight;
        }

        for (var i = 0; i < length; i++)
        {
            if (text[i] == '\r')
                continue;

            if (text[i] == '\n')
            {
                Linefeed();
                continue;
                /*if (text[i] == '\r' && i < length - 1 && text[i + 1] == '\n')
                    i += 1; // skip the '\n' associated to the '\r'*/
            }

            var size = MeasureString(previousCharacter, text[i]);

            if (currentLineWidth + size.X > maxWidth)
            {
                Linefeed();
            }

            currentLineWidth += size.X;
            currentLineHeight = Math.Max(currentLineHeight, size.Y);
            previousCharacter = text[i];
        }

        blockHeight += currentLineHeight;

        return new Vector2(Math.Max(currentLineWidth, blockWidth), blockHeight);
    }

    public Point MeasureString(char previousCharacter, char character)
    {
        var data = Font.Characters.ContainsKey(character) ? Font.Characters[character] : Font.DefaultCharacter;
        var width = data.XAdvance + GetKerning(previousCharacter, character) + Font.Spacing.X;
        var height = Math.Max(Font.LineHeight, data.Offset.Y + data.Bounds.Height);
        return new Point(width, height);
    }
}
