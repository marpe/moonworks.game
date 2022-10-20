namespace MyGame;

public class SpriteRenderer
{
    private readonly TextureSamplerBinding _samplerBinding;
    private Sprite _sprite;

    public SpriteRenderer(GraphicsDevice graphicsDevice, Sprite sprite)
    {
        var sampler = new Sampler(graphicsDevice, SamplerCreateInfo.PointClamp);
        _samplerBinding = new TextureSamplerBinding(sprite.Texture, sampler);
        _sprite = sprite;
    }

    public void Draw(CommandBuffer commandBuffer, SpriteBatch spriteBatch)
    {
        spriteBatch.Start(_samplerBinding);
        
        for(var i = 0; i < 10; i++)
        {
            spriteBatch.Add(_sprite, 0, Matrix3x2.Identity);
        }

        spriteBatch.PushVertexData(commandBuffer);
    }
}
