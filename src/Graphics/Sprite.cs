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
        TexturePage texturePage,
        Rect sliceRect,
        Rect frameRect
    )
    {
        TexturePage = texturePage;
        SliceRect = sliceRect;
        FrameRect = frameRect;
        UV = new UV(
            new Vector2((float)sliceRect.X / texturePage.Width, (float)sliceRect.Y / texturePage.Height),
            new Vector2((float)sliceRect.W / texturePage.Width, (float)sliceRect.H / texturePage.Height)
        );
    }
}
