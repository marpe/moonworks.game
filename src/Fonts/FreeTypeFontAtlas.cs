using FreeTypeSharp;

namespace MyGame.Fonts;

public class FontGlyph
{
    public int Index;
    public int Codepoint;
    public int FontSize;
    public Rectangle Bounds;
    public int XAdvance;
    public int XOffset;
    public int YOffset;
}

public class FreeTypeFontAtlas : IDisposable
{
    private static FreeTypeLibrary? _freeTypeLibrary;

    private FreeTypeFont? _font;
    private int _lineHeight;
    private int _ascent;
    private Dictionary<(int, int), int> _kernings = new();

    private Dictionary<int, FontGlyph> _glyphs = new();
    private Dictionary<int, Sprite> _sprites = new();
    private Texture? _texture;

    public FreeTypeFontAtlas(GraphicsDevice graphicsDevice, int width, int height, string fileName, uint fontSize,
        bool premultiplyAlpha)
    {
        var sw = Stopwatch.StartNew();
        _freeTypeLibrary ??= new FreeTypeLibrary();
        var packer = new RectPacker(width, height);

        var fontBytes = File.ReadAllBytes(fileName);
        _font = new FreeTypeFont(_freeTypeLibrary, fontBytes);

        // (charIndex, codepoint/charcode)
        var glyphList = GetGlyphList(_font);

        var atlasBuffer = new byte[packer.Width * packer.Height * 4];

        _font.GetMetricsForSize(fontSize, out var ascent, out var descent, out var lineHeight);

        _ascent = ascent;
        _lineHeight = lineHeight;

        foreach (var (charIndex, charcode) in glyphList)
        {
            if (!ProcessGlyph(fontSize, premultiplyAlpha, _font, charIndex, out var info, out var colorBuffer))
                continue;

            var dstX = 0;
            var dstY = 0;
            if (!packer.AddRect(info.Width, info.Height, ref dstX, ref dstY))
            {
                Logs.LogWarn("Font atlas full");
                break;
            }

            // setup destination rect
            var dstRect = new Rect(dstX, dstY, info.Width, info.Height);

            // render glyph to texture
            SetData(atlasBuffer, packer.Width, dstRect, colorBuffer);

            var glyph = new FontGlyph
            {
                Index = (int)charIndex,
                Bounds = new Rectangle(dstX, dstY, info.Width, info.Height),
                Codepoint = (int)charcode,
                XAdvance = (int)info.AdvanceX,
                XOffset = info.OffsetX,
                YOffset = info.OffsetY,
                FontSize = (int)fontSize,
            };
            _glyphs.Add((int)charcode, glyph);
        }

        _texture = Texture.CreateTexture2D(graphicsDevice, (uint)packer.Width, (uint)packer.Height,
            TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);
        SetTextureData(graphicsDevice, new TextureSlice(_texture), atlasBuffer);

        sw.StopAndLog("FontAtlas.AddFont");
    }

    public void DrawText(Renderer renderer, ReadOnlySpan<char> str, Vector2 position, Color color)
    {
        var offset = position;
        offset.Y += _ascent;
        FontGlyph? prevGlyph = null;
        foreach (var rune in str.EnumerateRunes())
        {
            var codepoint = rune.Value;

            if (!_glyphs.TryGetValue(codepoint, out var glyph))
            {
                offset.X += _glyphs['?'].XAdvance;
                continue;
            }

            if (prevGlyph != null)
            {
                if (!_kernings.TryGetValue((prevGlyph.Index, glyph.Index), out var kerning))
                {
                    kerning = _font!.GetGlyphKernAdvance(prevGlyph.Index, glyph.Index);
                    _kernings.Add((prevGlyph.Index, glyph.Index), kerning);
                }

                offset.X += kerning;
            }

            var transform = Matrix3x2.CreateTranslation(glyph.XOffset, glyph.YOffset) *
                            Matrix3x2.CreateScale(1, 1) *
                            Matrix3x2.CreateTranslation(offset);

            if (!_sprites.ContainsKey(codepoint))
            {
                _sprites.Add(codepoint, new Sprite(_texture!, glyph.Bounds));
            }

            renderer.DrawSprite(_sprites[codepoint], transform, color);
            offset.X += glyph.XAdvance;
            prevGlyph = glyph;
        }
    }

    private static void SetData(byte[] dstBuffer, int dstWidth, Rect dstRect, byte[] src)
    {
        var offset = dstRect.Y * dstWidth + dstRect.X;
        for (var y = 0; y < dstRect.H; y++)
        {
            for (var x = 0; x < dstRect.W; x++)
            {
                var si = (y * dstRect.W + x) * 4;
                var di = (offset + y * dstWidth + x) * 4;
                dstBuffer[di] = src[si];
                dstBuffer[di + 1] = src[si + 1];
                dstBuffer[di + 2] = src[si + 2];
                dstBuffer[di + 3] = src[si + 3];
            }
        }
    }

    private static HashSet<(uint, uint)> GetGlyphList(FreeTypeFont font)
    {
        var codePointMin = 0x0u;
        var codePointMax = 0x7fu;

        var glyphList = new HashSet<(uint, uint)>();
        for (var i = codePointMin; i < codePointMax; i++)
        {
            var charIndex = font.GetCharIndex(i);
            if (charIndex == 0)
            {
                continue;
            }

            glyphList.Add((charIndex, i));
        }

        return glyphList;
    }

    private static bool ProcessGlyph(uint fontSize, bool premultiplyAlpha, FreeTypeFont font, uint glyphIndex,
        out FreeTypeFont.GlyphInfo info,
        out byte[] colorBuffer)
    {
        // font.GetGlyphMetrics(glyphIndex, fontSize, out var metrics);

        var bitmap = font.RenderGlyphAndGetInfo(glyphIndex, fontSize, out info);

        if (bitmap.buffer == nint.Zero)
        {
            colorBuffer = Array.Empty<byte>();
            return false;
        }

        // copy native arr to managed
        var bufferSize = info.Width * info.Height;
        var buffer = new byte[bufferSize];
        Marshal.Copy(bitmap.buffer, buffer, 0, buffer.Length);

        // create a 4 channel buffer
        colorBuffer = new byte[bufferSize * 4];
        for (var i = 0; i < buffer.Length; i++)
        {
            var ci = i * 4;
            var c = buffer[i];
            if (premultiplyAlpha)
            {
                colorBuffer[ci] = colorBuffer[ci + 1] = colorBuffer[ci + 2] = colorBuffer[ci + 3] = c;
            }
            else
            {
                colorBuffer[ci] = colorBuffer[ci + 1] = colorBuffer[ci + 2] = 255;
                colorBuffer[ci + 3] = c;
            }
        }

        return true;
    }

    private static void SetTextureData(GraphicsDevice device, TextureSlice slice, byte[] data)
    {
        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetTextureData(slice, data);
        device.Submit(commandBuffer);
        device.Wait();
    }

    public void Dispose()
    {
        _font?.Dispose();
        _texture?.Dispose();
    }
}
