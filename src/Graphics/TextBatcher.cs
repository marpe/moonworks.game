﻿using System.Runtime.InteropServices;
using MoonWorks.Graphics.Font;
using WellspringCS;

namespace MyGame.Graphics;

public enum FontType
{
    Roboto,
    ConsolasMono
}

public class FontData
{
    public FontType Name;

    public TextBatch Batch;
    public Packer Packer;
    public Font Font;
    public Texture Texture;
    public TextureSamplerBinding Binding;
    public bool HasStarted;
}

public class TextBatcher
{
    public FontRange FontRange = new()
    {
        FirstCodepoint = 0x20,
        NumChars = 0x7e - 0x20 + 1,
        OversampleH = 0,
        OversampleV = 0
    };

    private uint _addCountSinceDraw = 0;

    private readonly Dictionary<FontType, FontData> _fonts = new();
    private GraphicsDevice _device;

    public TextBatcher(GraphicsDevice device)
    {
        _device = device;

        var fonts = new[]
        {
            (FontType.Roboto, ContentPaths.Fonts.RobotoRegularTtf),
            (FontType.ConsolasMono, ContentPaths.Fonts.ConsolaTtf)
        };

        var commandBuffer = device.AcquireCommandBuffer();
        foreach (var (key, path) in fonts)
        {
            var fontPath = Path.Combine(MyGameMain.ContentRoot, path);
            var font = new Font(fontPath);
            var fontPacker = new Packer(device, font, 18f, 512, 512, 2u);
            fontPacker.PackFontRanges(FontRange);
            fontPacker.SetTextureData(commandBuffer);
            var textBatchFont = new FontData()
            {
                Font = font,
                Packer = fontPacker,
                Name = key,
                Batch = new TextBatch(device),
                HasStarted = false,
            };
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
            _fonts[key].Binding = new TextureSamplerBinding(fontTexture, Renderer.PointClamp);
        }
    }

    public void Unload()
    {
        foreach (var (key, fontData) in _fonts)
        {
            fontData.Batch.Dispose();
            fontData.Packer.Dispose();
            fontData.Font.Dispose();
            fontData.Texture.Dispose();
        }

        _fonts.Clear();
    }

    public void Add(FontType fontTypeType, ReadOnlySpan<char> text, float x, float y, float depth, Color color, HorizontalAlignment alignH,
        VerticalAlignment alignV)
    {
        _addCountSinceDraw++;

        var font = _fonts[fontTypeType];
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
            return;

        foreach (var (key, font) in _fonts)
        {
            if (!font.HasStarted)
                continue;

            Wellspring.Wellspring_GetBufferData(
                font.Batch.Handle,
                out uint vertexCount,
                out IntPtr vertexDataPointer,
                out uint vertexDataLengthInBytes,
                out IntPtr indexDataPointer,
                out uint indexDataLengthInBytes
            );
            
            unsafe
            {
                var vertices = (Vertex*)vertexDataPointer.ToPointer();
                var sizeOfVert = Marshal.SizeOf<Vertex>();
                var numVerts = vertexDataLengthInBytes / sizeOfVert;
                
                var sprite = new Sprite();
                sprite.Texture = font.Texture;
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
                    spriteBatch.Draw(sprite, color, topLeftVert.Position.Z, transform, Renderer.PointClamp);
                }
            }

            font.HasStarted = false;
        }
        
        _addCountSinceDraw = 0;
    }
}