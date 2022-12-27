namespace MyGame.Graphics;

public static class TextureUtils
{
    public static bool EnsureTextureSize(ref Texture texture, GraphicsDevice device, Point size)
    {
        if (size.X == texture.Width && size.Y == texture.Height)
            return false;

        texture.Dispose();
        texture = Texture.CreateTexture2D(device, (uint)size.X, (uint)size.Y, texture.Format, texture.UsageFlags);
        return true;
    }

    public static Span<byte> ConvertSingleChannelTextureToRGBA(GraphicsDevice device, Texture texture)
    {
        if (texture.Format != TextureFormat.R8)
        {
            throw new InvalidOperationException("Expected texture format to be R8");
        }

        var pixelSize = (uint)Marshal.SizeOf<Color>();
        var buffer = Buffer.Create<byte>(device, BufferUsageFlags.Index, texture.Width * texture.Height * pixelSize);
        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.CopyTextureToBuffer(texture, buffer);
        device.Submit(commandBuffer);
        device.Wait();
        var pixels = new byte[buffer.Size];
        buffer.GetData(pixels, (uint)pixels.Length);

        var prevLength = pixels.Length;
        Array.Resize(ref pixels, pixels.Length * 4);

        for (var i = prevLength - 1; i >= 0; i--)
        {
            var p = pixels[i];
            pixels[i] = 0;
            pixels[i * 4] = 255;
            pixels[i * 4 + 1] = 255;
            pixels[i * 4 + 2] = 255;
            pixels[i * 4 + 3] = p;
        }

        return pixels;
    }

    public static unsafe Texture CreateTexture(GraphicsDevice device, uint width, uint height, Span<byte> pixels)
    {
        var texture = Texture.CreateTexture2D(device, width, height,
            TextureFormat.R8G8B8A8,
            TextureUsageFlags.Sampler
        );

        fixed (byte* p = pixels)
        {
            var commandBuffer = device.AcquireCommandBuffer();
            commandBuffer.SetTextureData(texture, (IntPtr)p, (uint)pixels.Length);
            device.Submit(commandBuffer);
            device.Wait();
        }

        return texture;
    }

    public static Texture PremultiplyAlpha(GraphicsDevice device, Texture texture)
    {
        var bytesPerPixel = (uint)Marshal.SizeOf<Color>();
        var buffer = Buffer.Create<byte>(device, BufferUsageFlags.Index, texture.Width * texture.Height * bytesPerPixel);

        var commandBuffer = device.AcquireCommandBuffer();
        commandBuffer.CopyTextureToBuffer(texture, buffer);
        device.Submit(commandBuffer);
        device.Wait();

        var pixels = new byte[buffer.Size];
        buffer.GetData(pixels, (uint)pixels.Length);

        PremultiplyAlpha(pixels);

        var newTexture = CreateTexture(device, texture.Width, texture.Height, pixels);

        return newTexture;
    }

    public static void PremultiplyAlpha(Span<byte> pixels)
    {
        for (var j = 0; j < pixels.Length; j += 4)
        {
            var alpha = pixels[j + 3];
            if (alpha == 255)
            {
                continue;
            }

            var a = alpha / 255f;
            pixels[j + 0] = (byte)(pixels[j + 0] * a);
            pixels[j + 1] = (byte)(pixels[j + 1] * a);
            pixels[j + 2] = (byte)(pixels[j + 2] * a);
        }
    }

    public static Texture CreateColoredTexture(GraphicsDevice device, uint width, uint height, Color color)
    {
        var texture = Texture.CreateTexture2D(device, width, height, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);
        Span<Color> data = new Color[width * height];
        data.Fill(color);
        var command = device.AcquireCommandBuffer();
        command.SetTextureData(texture, data.ToArray());
        device.Submit(command);
        device.Wait();
        return texture;
    }

    public static Texture LoadPngTexture(GraphicsDevice device, string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"File not found: {path}");
        }

        var commandBuffer = device.AcquireCommandBuffer();
        var texture = Texture.LoadPNG(device, commandBuffer, path);
        device.Submit(commandBuffer);
        device.Wait();
        return texture;
    }
    
    public static (Texture, AsepriteFile) LoadAseprite(GraphicsDevice device, ref CommandBuffer commandBuffer, string path)
    {
        var aseprite = AsepriteFile.LoadAsepriteFile(path);
        var data = AsepriteToTextureAtlasConverter.GetTextureData(aseprite);
        var texture = Texture.CreateTexture2D(
            device,
            aseprite.Header.Width * (uint)aseprite.Frames.Count,
            aseprite.Header.Height,
            TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler
        );
        commandBuffer.SetTextureData(texture, data);
        return (texture, aseprite);
    }
    
    public static Texture CreateTexture(GraphicsDevice device, Texture toCopy)
    {
        return Texture.CreateTexture2D(
            device,
            toCopy.Width,
            toCopy.Height,
            toCopy.Format,
            toCopy.UsageFlags | TextureUsageFlags.Sampler,
            toCopy.LevelCount
        );
    }
    
    public static void SaveTexture(string filename, GraphicsDevice device, Texture texture)
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
}
