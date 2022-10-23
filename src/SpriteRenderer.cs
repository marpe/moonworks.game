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

    public void Draw(CommandBuffer commandBuffer, SpriteBatch spriteBatch, Matrix3x2 transform, Color color, float depth)
    {
        spriteBatch.AddSingle(commandBuffer, _sprite, color, depth, transform);
    }
}
