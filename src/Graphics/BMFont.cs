using MyGame.BitmapFonts;

namespace MyGame.Graphics;

public class BMFont : IDisposable
{
    public struct DrawCall
    {
        public Sprite Sprite;
        public Matrix4x4 Transform;
        public Color[] Colors;
        public float Depth;
        public char Character;
    }
    
    private static Color[] _tempColors = new Color[4];

    public BitmapFont Font;

    public Texture[] Textures;

    public BMFont(BitmapFont font, Texture[] textures)
    {
        Font = font;
        Textures = textures;
    }

    public static BMFont LoadFromFile(GraphicsDevice device, string filename)
    {
        using var stream = File.OpenRead(filename);
        using var reader = new StreamReader(stream);
        var font = BitmapFont.LoadXml(reader);

        var textures = new Texture[font.Pages.Length];
        var directoryName = Path.GetDirectoryName(filename);
        for (var i = 0; i < textures.Length; i++)
        {
            var path = Path.Combine(directoryName ?? "", font.Pages[i].Filename);
            var texture = TextureUtils.LoadPngTexture(device, path);
            textures[i] = TextureUtils.PremultiplyAlpha(device, texture);
        }

        return new BMFont(font, textures);
    }

    public bool IsDisposed { get; private set; }

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


    public static Vector2 DrawInto(Renderer renderer, BMFont bmFont, ReadOnlySpan<char> text, Vector2 position,
        Vector2 origin,
        float rotation, Vector2 scale, Color color, float depth, DrawCall[] buffer, ref int startIndex)
    {
        _tempColors.AsSpan().Fill(color);
        return DrawInto(renderer, bmFont, text, position, origin, rotation, scale, _tempColors, depth, buffer, ref startIndex);
    }

    public static Vector2 DrawInto(Renderer renderer, BMFont bmFont, ReadOnlySpan<char> text, Vector2 position,
        Vector2 origin,
        float rotation, Vector2 scale, Color[] colors, float depth, DrawCall[] buffer, ref int startIndex)
    {
        var o = Matrix3x2.CreateTranslation(-origin.X, -origin.Y);
        var s = Matrix3x2.CreateScale(scale.X, scale.Y);
        var r = Matrix3x2.CreateRotation(rotation);
        var t = Matrix3x2.CreateTranslation(position.X, position.Y);
        var transformationMatrix = o * s * r * t;
        return DrawInto(renderer, bmFont, text, transformationMatrix, colors, depth, buffer, ref startIndex);
    }

    public static Vector2 DrawInto(Renderer renderer, BMFont bmFont, ReadOnlySpan<char> text, Matrix3x2 transform,
        Color color, float depth, DrawCall[] buffer, ref int startIndex)
    {
        _tempColors.AsSpan().Fill(color);
        return DrawInto(renderer, bmFont, text, transform, _tempColors, depth, buffer, ref startIndex);
    }
    
    public static Vector2 DrawInto(Renderer renderer, BMFont bmFont, ReadOnlySpan<char> text, Matrix3x2 transform,
        Color[] colors, float depth, DrawCall[] buffer, ref int startIndex)
    {
        var font = bmFont.Font;

        var previousCharacter = ' ';
        Character? currentChar = null;
        var offset = Vector2.Zero;

        var characterTransform = Matrix3x2.Identity;

        for (var i = 0; i < text.Length; ++i)
        {
            var c = text[i];

            if (c == '\r')
            {
                continue;
            }

            if (c == '\n')
            {
                offset.X = 0;
                offset.Y += font.LineHeight;
                currentChar = null;
                continue;
            }

            if (currentChar != null)
            {
                offset.X += font.Spacing.X + currentChar.XAdvance;
            }

            if (!font.Characters.TryGetValue(c, out currentChar))
            {
                currentChar = font.DefaultCharacter;
            }

            characterTransform.Translation =
                new Vector2(
                    offset.X + currentChar.Offset.X + bmFont.GetKerning(previousCharacter, currentChar.Char),
                    offset.Y + currentChar.Offset.Y
                );

            var sprite = new Sprite(bmFont.Textures[currentChar.TexturePage], currentChar.Bounds);
            buffer[startIndex].Sprite = sprite;
            buffer[startIndex].Transform = (characterTransform * transform).ToMatrix4x4();
            buffer[startIndex].Colors[0] = colors[0];
            buffer[startIndex].Colors[1] = colors[1];
            buffer[startIndex].Colors[2] = colors[2];
            buffer[startIndex].Colors[3] = colors[3];
            buffer[startIndex].Depth = depth;
            buffer[startIndex].Character = c;
            startIndex++;
            // renderer.DrawSprite(sprite, (characterTransform * transform).ToMatrix4x4(), colors, depth);

            previousCharacter = c;
        }

        if (currentChar != null)
        {
            offset.X += font.Spacing.X + currentChar.XAdvance;
        }

        return offset;
    }

    private int GetKerning(char previous, char current)
    {
        var kerning = new Kerning()
        {
            FirstCharacter = previous,
            SecondCharacter = current,
            Amount = 0,
        };
        if (!Font.Kernings.TryGetValue(kerning, out var result))
        {
            return 0;
        }

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
            {
                continue;
            }

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
