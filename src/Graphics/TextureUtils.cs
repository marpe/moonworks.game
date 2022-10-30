namespace MyGame.Graphics;

public static class TextureUtils
{
    
    public static void EnsureTextureSize(ref Texture texture, GraphicsDevice device, uint width, uint height)
    {
        if (width == texture.Width && height == texture.Height)
            return;

        texture.Dispose();
        texture = Texture.CreateTexture2D(device, width, height, texture.Format, texture.UsageFlags);
    }

    public static byte[] ConvertSingleChannelTextureToRGBA(GraphicsDevice device, Texture texture)
    {
        if (texture.Format != TextureFormat.R8)
            throw new InvalidOperationException("Expected texture format to be R8");

        var pixelSize = 8u;
        var buffer = MoonWorks.Graphics.Buffer.Create<byte>(device, BufferUsageFlags.Index, texture.Width * texture.Height * pixelSize);
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

    public static unsafe Texture CreateTexture(GraphicsDevice device, uint width, uint height, byte[] pixels)
    {
        var texture = Texture.CreateTexture2D(device, width, height,
            TextureFormat.R8G8B8A8,
            TextureUsageFlags.Sampler
        );
        var cmdBuffer = device.AcquireCommandBuffer();
        fixed (byte* p = pixels)
        {
            cmdBuffer.SetTextureData(texture, (IntPtr)p, (uint)pixels.Length);
            device.Submit(cmdBuffer);
        }

        return texture;
    }

    public static Texture CreateColoredTexture(GraphicsDevice device, uint width, uint height, Color color)
    {
        var texture = Texture.CreateTexture2D(device, 1, 1, TextureFormat.R8G8B8A8, TextureUsageFlags.Sampler);
        var command = device.AcquireCommandBuffer();
        Span<Color> data = new Color[width * height];
        data.Fill(color);
        command.SetTextureData(texture, data.ToArray());
        device.Submit(command);
        return texture;
    }
}
