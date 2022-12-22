namespace MyGame.Fonts;

public struct FontAtlasNode
{
    public int X;
    public int Y;
    public int Width;
}

public class FreeTypeFontAtlas
{
    public Texture? Texture;
    private GraphicsDevice _graphicsDevice;
    public FontAtlasNode[] Nodes;
    public int NodesNumber;
    public int Height;
    public int Width;

    public FreeTypeFontAtlas(GraphicsDevice graphicsDevice, int width, int height)
    {
        _graphicsDevice = graphicsDevice;

        Width = width;
        Height = height;

        Nodes = new FontAtlasNode[256];
        Nodes[0].X = 0;
        Nodes[0].Y = 0;
        Nodes[0].Width = Width;
        NodesNumber++;
    }

    public void InsertNode(int idx, int x, int y, int w)
    {
        if (NodesNumber + 1 > Nodes.Length)
        {
            var oldNodes = Nodes;
            var newLength = Nodes.Length == 0 ? 8 : Nodes.Length * 2;
            Nodes = new FontAtlasNode[newLength];
            for (var i = 0; i < oldNodes.Length; ++i)
            {
                Nodes[i] = oldNodes[i];
            }
        }

        for (var i = NodesNumber; i > idx; i--)
        {
            Nodes[i] = Nodes[i - 1];
        }

        Nodes[idx].X = x;
        Nodes[idx].Y = y;
        Nodes[idx].Width = w;
        NodesNumber++;
    }

    public void RemoveNode(int idx)
    {
        if (NodesNumber == 0)
        {
            return;
        }

        for (var i = idx; i < NodesNumber - 1; i++)
        {
            Nodes[i] = Nodes[i + 1];
        }

        NodesNumber--;
    }

    public void Reset(int w, int h)
    {
        Width = w;
        Height = h;
        NodesNumber = 0;
        Nodes[0].X = 0;
        Nodes[0].Y = 0;
        Nodes[0].Width = w;
        NodesNumber++;
    }

    public bool AddSkylineLevel(int idx, int x, int y, int w, int h)
    {
        InsertNode(idx, x, y + h, w);
        for (var i = idx + 1; i < NodesNumber; i++)
        {
            if (Nodes[i].X < Nodes[i - 1].X + Nodes[i - 1].Width)
            {
                var shrink = Nodes[i - 1].X + Nodes[i - 1].Width - Nodes[i].X;
                Nodes[i].X += shrink;
                Nodes[i].Width -= shrink;
                if (Nodes[i].Width <= 0)
                {
                    RemoveNode(i);
                    i--;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        for (var i = 0; i < NodesNumber - 1; i++)
        {
            if (Nodes[i].Y == Nodes[i + 1].Y)
            {
                Nodes[i].Width += Nodes[i + 1].Width;
                RemoveNode(i + 1);
                i--;
            }
        }

        return true;
    }

    public int RectFits(int i, int w, int h)
    {
        var x = Nodes[i].X;
        var y = Nodes[i].Y;
        if (x + w > Width)
        {
            return -1;
        }

        var spaceLeft = w;
        while (spaceLeft > 0)
        {
            if (i == NodesNumber)
            {
                return -1;
            }

            y = Math.Max(y, Nodes[i].Y);
            if (y + h > Height)
            {
                return -1;
            }

            spaceLeft -= Nodes[i].Width;
            ++i;
        }

        return y;
    }

    public bool AddRect(int rw, int rh, ref int rx, ref int ry)
    {
        var besth = Height;
        var bestw = Width;
        var besti = -1;
        var bestx = -1;
        var besty = -1;
        for (var i = 0; i < NodesNumber; i++)
        {
            var y = RectFits(i, rw, rh);
            if (y != -1)
            {
                if (y + rh < besth || (y + rh == besth && Nodes[i].Width < bestw))
                {
                    besti = i;
                    bestw = Nodes[i].Width;
                    besth = y + rh;
                    bestx = Nodes[i].X;
                    besty = y;
                }
            }
        }

        if (besti == -1)
        {
            return false;
        }

        if (!AddSkylineLevel(besti, bestx, besty, rw, rh))
        {
            return false;
        }

        rx = bestx;
        ry = besty;
        return true;
    }

    public void AddFont(string fileName, uint fontSize = 24u, bool premultiplyAlpha = true)
    {
        var sw = Stopwatch.StartNew();
        var fontBytes = File.ReadAllBytes(fileName);
        var font = new FreeTypeFont(fontBytes);

        var glyphList = GetGlyphList(font);

        Texture = Texture.CreateTexture2D(_graphicsDevice, (uint)Width, (uint)Height, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);

        foreach (var glyphId in glyphList)
        {
            if (!ProcessGlyph(fontSize, premultiplyAlpha, font, glyphId, out var info, out var colorBuffer))
                continue;

            var dstX = 0; var dstY = 0;
            if (!AddRect(info.Width, info.Height, ref dstX, ref dstY))
            {
                Logs.LogWarn("Font atlas full");
                break;
            }

            // setup destination rect
            var dstRect = new Rect(dstX, dstY, info.Width, info.Height);

            // render glyph to texture
            SetTextureData(_graphicsDevice, new TextureSlice(Texture, dstRect), colorBuffer);
        }

        // SaveTexture("atlas.png", _graphicsDevice, Texture);

        sw.StopAndLog("FontAtlas.AddFont");
    }

    private static HashSet<uint> GetGlyphList(FreeTypeFont font)
    {
        var codePointMin = 0x0u;
        var codePointMax = 0x7fu;

        var glyphList = new HashSet<uint>();
        for (var i = codePointMin; i < codePointMax; i++)
        {
            var glyphId = font.GetCharIndex(i);
            if (glyphId == 0)
            {
                continue;
            }

            glyphList.Add(glyphId);
        }

        return glyphList;
    }

    private static bool ProcessGlyph(uint fontSize, bool premultiplyAlpha, FreeTypeFont font, uint glyphId, out FreeTypeFont.GlyphInfo info,
        out byte[] colorBuffer)
    {
        font.GetGlyphMetrics(glyphId, fontSize, out var metrics);

        var bitmap = font.RenderGlyphAndGetInfo(glyphId, fontSize, out info);

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
