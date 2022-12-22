using Rect = MoonWorks.Graphics.Rect;

namespace MyGame.Fonts;

public class FreeTypeFontAtlas
{
    public Texture? Texture;
    private GraphicsDevice _graphicsDevice;

    public FreeTypeFontAtlas(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
    }

    public void AddFont(string fileName, uint fontSize = 18u, uint atlasWidth = 512u, uint atlasHeight = 512u)
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
        }

        Texture = Texture.CreateTexture2D(_graphicsDevice, atlasWidth, atlasHeight, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);

        var offsetX = 0;
        var offsetY = 0;

        var maxY = 0;

        foreach (var glyphId in glyphList)
        {
            font.GetGlyphMetrics(glyphId, fontSize, out var metrics);

            var bitmap = font.RenderGlyphAndGetInfo(glyphId, fontSize, out var info);

            if (bitmap.buffer == nint.Zero)
                continue;

            // copy native arr to managed
            var bufferSize = info.Width * info.Height;
            var buffer = new byte[bufferSize];
            Marshal.Copy(bitmap.buffer, buffer, 0, buffer.Length);

            // create a 4 channel buffer
            var colorBuffer = new byte[bufferSize * 4];
            for (var i = 0; i < buffer.Length; i++)
            {
                var ci = i * 4;
                var c = buffer[i];
                colorBuffer[ci] = colorBuffer[ci + 1] = colorBuffer[ci + 2] = colorBuffer[ci + 3] = c;
            }

            // setup destination rect
            var dstRect = new Rect(offsetX, offsetY, info.Width, info.Height);
            maxY = Math.Max(maxY, info.Height);

            if (Texture.Width < dstRect.X + dstRect.W)
            {
                offsetY += maxY;
                offsetX = 0;
                maxY = 0;

                dstRect.Y = offsetY;
                dstRect.X = offsetX;
            }

            if (Texture.Height < dstRect.Y + dstRect.H)
            {
                break;
            }

            // render glyph to texture
            SetTextureData(_graphicsDevice, new TextureSlice(Texture, dstRect), colorBuffer);

            offsetX += info.Width;
        }

        SaveTexture("atlas.png", _graphicsDevice, Texture);

        sw.StopAndLog("FontAtlas.AddFont");
    }

    private static void SaveTexture(string filename, GraphicsDevice device, Texture texture)
    {
        var tempBuffer = Buffer.Create<byte>(device, BufferUsageFlags.Index, texture.Width * texture.Height * sizeof(uint));
        var tempPixels = new byte[tempBuffer.Size];

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.CopyTextureToBuffer(texture, tempBuffer);
        device.Submit(commandBuffer);
        device.Wait();

        tempBuffer.GetData(tempPixels, tempBuffer.Size);
        Texture.SavePNG(filename, (int)texture.Width, (int)texture.Height, texture.Format, tempPixels);
    }

    private static void SetTextureData(GraphicsDevice device, TextureSlice slice, byte[] data)
    {
        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.SetTextureData(slice, data);
        device.Submit(commandBuffer);
        device.Wait();
    }
}
