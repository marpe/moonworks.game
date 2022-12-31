namespace MyGame.Debug;

public class TestFunctions
{
    /// <summary>
    /// https://gist.github.com/d7samurai/9f17966ba6130a75d1bfb0f1894ed377
    /// </summary>
    private static void DrawPixelArtShaderTestSkull(Renderer renderer, Vector2 prevPosition, Vector2 position, double alpha)
    {
        Matrix4x4 GetTransform(Point size, Vector2 pivot, Vector2 prevPosition, Vector2 position, Vector2 squash, double alpha)
        {
            var ssquash = Matrix3x2.CreateTranslation(-size * pivot) *
                          Matrix3x2.CreateScale(squash) *
                          Matrix3x2.CreateTranslation(size * pivot);

            var xform = Matrix3x2.CreateTranslation(pivot * (size - World.DefaultGridSize)) *
                        ssquash *
                        Matrix3x2.CreateTranslation(Vector2.Lerp(prevPosition, position, (float)alpha));

            return xform.ToMatrix4x4();
        }
        
        var ts = ((MathF.Sin(Shared.Game.Time.TotalElapsedTime) + 1.0f) * 0.5f) * 5f + 5.0f;
        var xform = GetTransform(new Point(16, 16), new Vector2(0.5f, 0.5f), position, prevPosition, new Vector2(ts), alpha);
        var sprite = ContentPaths.animations.skull_aseprite;
        var texture = Shared.Content.Load<TextureAsset>(sprite).TextureSlice.Texture;
        renderer.DrawSprite(texture, xform, Color.White, 0, SpriteFlip.None);
    }
}
