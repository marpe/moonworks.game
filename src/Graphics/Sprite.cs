namespace MyGame.Graphics;

public struct Sprite
{
    public TexturePage TexturePage { get; }
    public Texture Texture => TexturePage.Texture;
    public Rect SliceRect { get; } // the pixel slice on the texture page
    public Rect FrameRect { get; } // offset and original width to reproduce the transparency
    public UV UV { get; }

    public Sprite(Texture texture)
    {
        TexturePage = new TexturePage(texture);
        SliceRect = new Rect(0, 0, (int)texture.Width, (int)texture.Height);
        FrameRect = SliceRect;
        UV = new UV(
            new Vector2((float)SliceRect.X / TexturePage.Width, (float)SliceRect.Y / TexturePage.Height),
            new Vector2((float)SliceRect.W / TexturePage.Width, (float)SliceRect.H / TexturePage.Height)
        );
    }
    
    public Sprite(
        Texture texture,
        Rect sliceRect,
        Rect frameRect
    )
    {
        TexturePage = new TexturePage(texture);
        SliceRect = sliceRect;
        FrameRect = frameRect;
        UV = new UV(
            new Vector2((float)sliceRect.X / TexturePage.Width, (float)sliceRect.Y / TexturePage.Height),
            new Vector2((float)sliceRect.W / TexturePage.Width, (float)sliceRect.H / TexturePage.Height)
        );
    }
}
