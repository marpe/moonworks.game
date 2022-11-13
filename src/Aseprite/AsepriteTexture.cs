namespace MyGame.Aseprite;

public class AsepriteTexture : IDisposable
{
    public AsepriteFile File;
    public string FilePath;
    public Rectangle[] SourceRectangles;
    public Texture Texture;

    public AsepriteTexture(Rectangle[] sourceRects, Texture texture, AsepriteFile asepriteFile, string filepath)
    {
        SourceRectangles = sourceRects;
        Texture = texture;
        File = asepriteFile;
        FilePath = filepath;
    }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool isDisposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (isDisposing)
        {
            Texture.Dispose();
        }

        IsDisposed = true;
    }
}
