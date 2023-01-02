namespace MyGame.Debug;

public class TestFunctions
{
    /// <summary>
    /// https://gist.github.com/d7samurai/9f17966ba6130a75d1bfb0f1894ed377
    /// </summary>
    public static void DrawPixelArtShaderTestSkull(Renderer renderer, ref CommandBuffer commandBuffer, Texture renderDestination, Vector2 position)
    {
        var texture = Shared.Content.Load<TextureAsset>(ContentPaths.animations.skull_aseprite).TextureSlice.Texture;

        var minScale = 4f;
        var maxScale = 30f;
        var texScaleT = ((MathF.Sin(Shared.Game.World.WorldTotalElapsedTime * 0.5f) + 1.0f) * 0.5f);
        var texScale = MathF.Lerp(minScale, maxScale, texScaleT);
        var texSize = texture.Size().ToVec2() * texScale;
        var min = position - texSize * 0.5f;
        var max = position + texSize * 0.5f;
        var size = max - min;

        var dstRect1 = new Bounds(min.X, min.Y, size.X * 0.5f, size.Y);
        var dstRect2 = new Bounds(min.X + size.X * 0.5f + 1, min.Y, size.X * 0.5f, size.Y);

        var srcRect1 = new Bounds(0, 0, texture.Width * 0.5f, texture.Height);
        var srcRect2 = new Bounds(texture.Width * 0.5f, 0, texture.Width * 0.5f, texture.Height);
        
        // renderer.DrawRect((Rectangle)dstRect1, Color.Blue);
        renderer.DrawSprite(texture, srcRect1, dstRect1, Color.White, 0, SpriteFlip.None);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, null, true, PipelineType.Sprite);

        // renderer.DrawRect((Rectangle)dstRect2, Color.Green);
        renderer.DrawSprite(texture, srcRect2, dstRect2, Color.White, 0, SpriteFlip.None);
        var viewProjection = Renderer.GetOrthographicProjection(renderDestination.Width, renderDestination.Height);
        renderer.RunRenderPass(ref commandBuffer, renderDestination, null, viewProjection, false, PipelineType.PixelArt);
    }
}
