namespace MyGame;

public class SpriteRenderer
{
    private Sprite _sprite;

    public SpriteRenderer(Texture texture)
    {
        _sprite = new Sprite(texture);
    }
    
    public SpriteRenderer(Sprite sprite)
    {
        _sprite = sprite;
    }

    public void Draw(CommandBuffer commandBuffer, SpriteBatch spriteBatch, Matrix3x2 transform, Color color, float depth, Sampler sampler)
    {
        spriteBatch.Start(new TextureSamplerBinding(_sprite.Texture, sampler));
        spriteBatch.Add(_sprite, color, depth, transform);
        spriteBatch.PushVertexData(commandBuffer);
    }
}
