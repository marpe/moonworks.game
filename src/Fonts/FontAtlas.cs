namespace MyGame.Fonts;

public class FontAtlas
{
    public byte[] Buffer = Array.Empty<byte>();
    public Texture? Texture;
    private GraphicsDevice _graphicsDevice;

    public FontAtlas(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    private void RenderGlyph(FreeTypeFont font, int glyphId)
    {
        // render glyph to byte buffer
        
        // create atlas texture
        
        // erase area where we place glyph
        
        // font.RasterizeGlyphBitmap();
    }

    public void AddFont(string fileName)
    {
        var sw = Stopwatch.StartNew();
        var fontBytes = File.ReadAllBytes(fileName);
        var font = new FreeTypeFont(fontBytes);

        var codePointMin = 0x0u;
        var codePointMax = 0x7fu;

        var glyphList = new HashSet<uint>();
        for (var i = codePointMin; i < codePointMax; i++)
        {
            var glyphId = font.GetCharIndex(i);
            if (glyphId == 0)
                continue;
            glyphList.Add(glyphId);
            Logs.LogInfo($"GlyphId: {glyphId}");
        }

        foreach (var glyphId in glyphList)
        {
            font.GetGlyphMetrics(glyphId, 12u, out var metrics);
            // if metrics == null continue
            
            // render glyph into a bitmap
            var bitmap = font.RenderGlyphAndGetInfo(glyphId, 12u, out var info);
        }

        sw.StopAndLog("FontAtlas.AddFont");
    }
}
