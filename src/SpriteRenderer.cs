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

    public void Draw(CommandBuffer commandBuffer, SpriteBatch spriteBatch, Sampler sampler)
    {
        spriteBatch.Start(new TextureSamplerBinding(_sprite.Texture, sampler));
        
        for(var i = 0; i < 10; i++)
        {
            spriteBatch.Add(_sprite, Color.White, 0, Matrix3x2.CreateTranslation(i * 64, 0));
        }

        spriteBatch.PushVertexData(commandBuffer);
    }
}
