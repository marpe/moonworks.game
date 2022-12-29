namespace MyGame.Graphics;

public class RenderTarget
{
    public Texture Target;

    public uint Width => Target.Width;
    public uint Height => Target.Height;

    public UPoint Size => new UPoint(Width, Height);

    public RenderTarget(Texture target)
    {
        Target = target;
    }

    public static implicit operator Texture(RenderTarget rt) => rt.Target;
    public static implicit operator Sprite(RenderTarget rt) => new(rt.Target);
}
