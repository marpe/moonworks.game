﻿using MyGame.Fonts;

namespace MyGame.Graphics;

public struct GfxPipeline
{
    public GraphicsPipeline Pipeline;
    public GraphicsPipelineCreateInfo CreateInfo;
    public string VertexShaderPath;
    public string FragmentShaderPath;
}

public class Renderer
{
    public BMFont[] BMFonts { get; }


    private readonly Sprite _blankSprite;
    private readonly Texture _blankTexture;
    public Sprite BlankSprite => _blankSprite;

    private readonly GraphicsDevice _device;

    private readonly MyGameMain _game;

    public readonly SpriteBatch SpriteBatch;

    private ColorAttachmentInfo _colorAttachmentInfo;

    public Color DefaultClearColor = Color.CornflowerBlue;

    private DepthStencilAttachmentInfo _depthStencilAttachmentInfo;

    public Dictionary<UPoint, Texture> _depthTextureCache = new();

    public Dictionary<PipelineType, GfxPipeline> Pipelines;

    public FreeTypeFontAtlas FreeTypeFontAtlas;
    private static Color[] _tempColors = new Color[4];
    public int RenderPasses;

    public Renderer(MyGameMain game)
    {
        var createRendererTimer = Stopwatch.StartNew();
        _game = game;
        _device = game.GraphicsDevice;

        SpriteBatch = new SpriteBatch(_device);

        _blankTexture = TextureUtils.CreateColoredTexture(game.GraphicsDevice, 1, 1, Color.White);
        _blankSprite = new Sprite(_blankTexture);

        var bmFontTimer = Stopwatch.StartNew();
        BMFonts = CreateBMFonts(_device);
        bmFontTimer.StopAndLog("BMFonts");

        var freeTypeTimer = Stopwatch.StartNew();
        FreeTypeFontAtlas = new FreeTypeFontAtlas(game.GraphicsDevice, 512, 512, ContentPaths.fonts.consola_ttf, 18u, true);
        freeTypeTimer.StopAndLog("FreeType");

        _depthStencilAttachmentInfo = new DepthStencilAttachmentInfo()
        {
            DepthStencilClearValue = new DepthStencilValue(0, 0),
            Texture = null,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            StencilLoadOp = LoadOp.Clear,
            StencilStoreOp = StoreOp.Store,
        };

        _colorAttachmentInfo = new ColorAttachmentInfo()
        {
            ClearColor = Color.CornflowerBlue,
            LoadOp = LoadOp.Clear,
        };

        var pipelinesTimer = Stopwatch.StartNew();
        Pipelines = Graphics.Pipelines.CreatePipelines(_device);
        pipelinesTimer.StopAndLog("Pipelines");
        createRendererTimer.StopAndLog("Renderer");
    }

    private static BMFont[] CreateBMFonts(GraphicsDevice device)
    {
        var bmFontTypes = new[]
        {
            (BMFontType.ConsolasMonoSmall, ContentPaths.bmfonts.consolas_fnt),
            (BMFontType.ConsolasMonoMedium, ContentPaths.bmfonts.consolas48_fnt),
            (BMFontType.ConsolasMonoLarge, ContentPaths.bmfonts.consolas60_fnt),
            (BMFontType.ConsolasMonoHuge, ContentPaths.bmfonts.consolas72_fnt),
            (BMFontType.PixellariLarge, ContentPaths.bmfonts.pixellari48_fnt),
            (BMFontType.PixellariHuge, ContentPaths.bmfonts.pixellari72_fnt),
        };

        var fonts = new BMFont[bmFontTypes.Length];
        for (var i = 0; i < bmFontTypes.Length; i++)
        {
            var (type, path) = bmFontTypes[i];
            fonts[i] = Shared.Content.Load<BMFont>(path);
        }

        return fonts;
    }

    public BMFont GetFont(BMFontType fontType)
    {
        return BMFonts[(int)fontType];
    }

    public void DrawPoint(Vector2 position, Color color, float size = 1.0f, float depth = 0)
    {
        var t = Matrix3x2.CreateTranslation(-0.5f, -0.5f) *
                Matrix3x2.CreateScale(size, size) *
                Matrix3x2.CreateTranslation(position.X, position.Y);
        DrawSprite(_blankSprite, t, color);
    }

    public void DrawRect(Vector2 min, Vector2 max, Color color, float depth = 0)
    {
        var t = Matrix3x2.CreateScale(max.X - min.X, max.Y - min.Y) *
                Matrix3x2.CreateTranslation(min.X, min.Y);
        DrawSprite(_blankSprite, t, color);
    }

    public void DrawRect(in Rectangle rect, Color color, float depth = 0)
    {
        var t = Matrix3x2.CreateScale(rect.Width, rect.Height) * 
                Matrix3x2.CreateTranslation(rect.X, rect.Y);
        DrawSprite(_blankSprite, t, color);
    }

    public void DrawCircleOutline(Vector2 position, float radius, Color color, float thickness, int numSegments = 12)
    {
        var prevPoint = position + new Vector2(radius, 0);
        for (var i = 1; i <= numSegments; i++)
        {
            var a = MathHelper.TwoPi * i / (float)numSegments;
            var currentPoint = position + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
            DrawLine(prevPoint, currentPoint, color, thickness);
            prevPoint = currentPoint;
        }
    }

    public void DrawLine(Vector2 from, Vector2 to, Color color, float thickness = 1.0f)
    {
        var length = (from - to).Length();
        var origin = Matrix3x2.CreateTranslation(0, 0);
        var scale = Matrix3x2.CreateScale(length, thickness);
        var rotation = Matrix3x2.CreateRotation(MathF.AngleBetweenVectors(from, to));
        var translation = Matrix3x2.CreateTranslation(from);
        var tAll = origin * scale * rotation * translation;
        DrawSprite(_blankSprite, tAll, color);
    }

    public void DrawRectWithOutline(Vector2 min, Vector2 max, Color color, Color outlineColor, float thickness = 1.0f)
    {
        DrawRect(min, max, color);
        DrawRectOutline(min, max, outlineColor, thickness);
    }

    public void DrawRectWithOutline(in Rectangle rectangle, Color color, Color outlineColor)
    {
        DrawRect(rectangle, color);
        DrawRectOutline(rectangle, outlineColor);
    }

    public void DrawRectOutline(in Rectangle rectangle, Color color, float thickness = 1.0f)
    {
        DrawRectOutline(rectangle.Min().ToVec2(), rectangle.Max().ToVec2(), color, thickness);
    }

    public void DrawRectOutline(in Bounds bounds, Color color, float thickness = 1.0f)
    {
        DrawRectOutline(bounds.Min, bounds.Max, color, thickness);
    }

    public void DrawRectOutline(Vector2 min, Vector2 max, Color color, float thickness = 1.0f)
    {
        ReadOnlySpan<Vector2> points = stackalloc Vector2[]
        {
            new(min.X + thickness, min.Y),
            new(max.X, min.Y),

            new(max.X, min.Y + thickness),
            new(max.X, max.Y - thickness),

            new(max.X, max.Y),
            new(min.X, max.Y),

            new(min.X, max.Y - thickness),
            new(min.X, min.Y),
        };
        for (var i = 0; i < points.Length; i += 2)
        {
            DrawLine(points[i], points[i + 1], color, thickness);
        }
    }

    public void DrawSprite(in Sprite sprite, in Matrix3x2 transform, Color color, SpriteFlip flip = SpriteFlip.None)
    {
        DrawSprite(sprite, ToMatrix4x4(transform), color, flip);
    }

    public void DrawSprite(in Sprite sprite, in Matrix4x4 transform, Color color, SpriteFlip flip = SpriteFlip.None)
    {
        SetColor(ref _tempColors, color);
        DrawSprite(sprite, transform, _tempColors, 0, flip);
    }

    public void DrawSprite(in Sprite sprite, Matrix4x4 transform, Color[] colors, float depth = 0, SpriteFlip flip = SpriteFlip.None)
    {
        var min = new Vector2(0, 0);
        var max = new Vector2(sprite.SrcRect.Width, sprite.SrcRect.Height);
        Vector2.Transform(ref min, ref transform, out min);
        Vector2.Transform(ref max, ref transform, out max);
        var dstRect = new Bounds(min, max);
        SpriteBatch.Draw(sprite.TextureSlice.Texture, sprite.UV, dstRect, colors, depth, flip);
    }
    
    public void DrawSprite(Texture texture, in UV uvs, in Bounds dstRect, Color color, float depth = 0, SpriteFlip flip = SpriteFlip.None)
    {
        SetColor(ref _tempColors, color);
        SpriteBatch.Draw(texture, uvs, dstRect, _tempColors, depth, flip);
    }

    public void DrawSprite(Texture texture, in Bounds? srcRect, in Bounds dstRect, Color color, float depth = 0, SpriteFlip flip = SpriteFlip.None)
    {
        SetColor(ref _tempColors, color);
        DrawSprite(texture, srcRect, dstRect, _tempColors, depth, flip);
    }
    
    public void DrawSprite(Texture texture, in Bounds? srcRect, in Bounds dstRect, Color[] colors, float depth = 0, SpriteFlip flip = SpriteFlip.None)
    {
        SpriteBatch.Draw(texture, srcRect ?? new Bounds(0, 0, texture.Width, texture.Height), dstRect, colors, depth, flip);
    }
    
    public void DrawFTText(BMFontType fontType, ReadOnlySpan<char> text, Vector2 position, Color color)
    {
        FreeTypeFontAtlas.DrawText(this, text, position, color);
    }

    public void DrawBMText(BMFontType fontType, ReadOnlySpan<char> text, Vector2 position, Vector2 origin, Vector2 scale, float rotation, float depth,
        Color color, BMFont.DrawCall[] buffer, ref int startIndex)
    {
        BMFont.DrawInto(this, BMFonts[(int)fontType], text, position, origin, rotation, scale, color, depth, buffer, ref startIndex);
    }

    public void DrawBMText(BMFontType fontType, ReadOnlySpan<char> text, Vector2 position, Vector2 origin, Vector2 scale, float rotation, float depth,
        Color[] colors, BMFont.DrawCall[] buffer, ref int startIndex)
    {
        BMFont.DrawInto(this, BMFonts[(int)fontType], text, position, origin, rotation, scale, colors, depth, buffer, ref startIndex);
    }

    public Vector2 MeasureString(BMFontType fontType, ReadOnlySpan<char> text)
    {
        // TODO (marpe): Replace with FreeType
        var font = GetFont(fontType);
        return font.MeasureString(text);
    }

    public (CommandBuffer, Texture?) AcquireSwapchainTexture()
    {
        var commandBuffer = _device.AcquireCommandBuffer();
        var swapTexture = commandBuffer.AcquireSwapchainTexture(_game.MainWindow);
        return (commandBuffer, swapTexture);
    }

    public void Clear(ref CommandBuffer commandBuffer, Texture renderTarget, Color clearColor)
    {
        var cai = new ColorAttachmentInfo()
        {
            ClearColor = clearColor,
            LoadOp = LoadOp.Clear,
            Texture = renderTarget,
        };
        commandBuffer.BeginRenderPass(cai);
        commandBuffer.EndRenderPass();
    }

    public void UpdateBuffers(ref CommandBuffer commandBuffer)
    {
        SpriteBatch.UpdateBuffers(ref commandBuffer);
    }

    public void BeginRenderPass(ref CommandBuffer commandBuffer, Texture renderTarget, Color? clearColor, PipelineType pipelineType)
    {
        RenderPasses++;
        _colorAttachmentInfo.Texture = renderTarget;
        _colorAttachmentInfo.LoadOp = clearColor == null ? LoadOp.Load : LoadOp.Clear;
        _colorAttachmentInfo.ClearColor = clearColor ?? DefaultClearColor;

        var pipeline = Pipelines[pipelineType];
        if (pipeline.CreateInfo.AttachmentInfo.HasDepthStencilAttachment)
            commandBuffer.BeginRenderPass(_depthStencilAttachmentInfo, _colorAttachmentInfo);
        else
            commandBuffer.BeginRenderPass(_colorAttachmentInfo);
        commandBuffer.BindGraphicsPipeline(pipeline.Pipeline);
    }

    public void RunRenderPass(ref CommandBuffer commandBuffer, Texture renderTarget, Color? clearColor, Matrix4x4? viewProjection, bool usePointFiltering = true,
        PipelineType pipeline = PipelineType.Sprite)
    {
        UpdateBuffers(ref commandBuffer);
        BeginRenderPass(ref commandBuffer, renderTarget, clearColor, pipeline);
        DrawIndexedSprites(ref commandBuffer, viewProjection, usePointFiltering);
        EndRenderPass(ref commandBuffer);
    }


    public void EndRenderPass(ref CommandBuffer commandBuffer)
    {
        commandBuffer.EndRenderPass();
    }
    
    public void DrawIndexedSprites(ref CommandBuffer commandBuffer, Matrix4x4? viewProjection, bool usePointFiltering)
    {
        var vertUniforms = viewProjection ?? GetOrthographicProjection(_colorAttachmentInfo.Texture.Width, _colorAttachmentInfo.Texture.Height);
        SpriteBatch.DrawIndexed(ref commandBuffer, vertUniforms, usePointFiltering);
    }

    public void Submit(ref CommandBuffer commandBuffer)
    {
        _device.Submit(commandBuffer);
    }

    public void Unload()
    {
        for (var i = 0; i < BMFonts.Length; i++)
        {
            BMFonts[i].Dispose();
        }

        foreach (var (_, pipeline) in Pipelines)
        {
            pipeline.Pipeline.Dispose();
        }

        Pipelines.Clear();
        _blankTexture.Dispose();
        SpriteBatch.Unload();

        foreach (var (_, texture) in _depthTextureCache)
        {
            texture.Dispose();
        }

        _depthTextureCache.Clear();
    }

    public static (Matrix3x2, Rectangle) GetViewportTransform(Point screenResolution, Point designResolution)
    {
        var scaleUniform = Math.Min(
            screenResolution.X / (float)designResolution.X,
            screenResolution.Y / (float)designResolution.Y
        );

        var renderSize = new Point(
            (int)(scaleUniform * designResolution.X),
            (int)(scaleUniform * designResolution.Y)
        );

        var offset = new Point(
            (int)((screenResolution.X - renderSize.X) * 0.5f),
            (int)((screenResolution.Y - renderSize.Y) * 0.5f)
        );

        var transform = Matrix3x2.CreateScale(scaleUniform, scaleUniform) *
                        Matrix3x2.CreateTranslation(offset.X, offset.Y);

        return (transform, new Rectangle(offset.X, offset.Y, renderSize.X, renderSize.Y));
    }

    public static Matrix4x4 GetOrthographicProjection(uint width, uint height)
    {
        return Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, 0, 10000f);
    }

    private static void DrawLetterAndPillarBoxes(Renderer renderer, Point screenSize, Rectangle viewport, Color color)
    {
        if (screenSize.X != viewport.Width)
        {
            var left = new Rectangle(0, 0, viewport.X, viewport.Height);
            var right = new Rectangle(viewport.X + viewport.Width, 0, screenSize.X - (viewport.X + viewport.Width), screenSize.Y);
            renderer.DrawRect(left, color);
            renderer.DrawRect(right, color);
        }

        if (screenSize.Y != viewport.Height)
        {
            var top = new Rectangle(0, 0, screenSize.X, viewport.Y);
            var bottom = new Rectangle(0, viewport.Y + viewport.Height, screenSize.X, screenSize.Y - (viewport.Y + viewport.Height));
            renderer.DrawRect(top, color);
            renderer.DrawRect(bottom, color);
        }
    }
    
    private static void SetColor(ref Color[] colors, Color color)
    {
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = color;
        }
    }

    private static Matrix4x4 ToMatrix4x4(in Matrix3x2 matrix)
    {
        return new Matrix4x4(
            matrix.M11, matrix.M12, 0, 0,
            matrix.M21, matrix.M22, 0, 0,
            0, 0, 1, 0,
            matrix.M31, matrix.M32, 0, 1
        );
    }
}
