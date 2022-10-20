namespace MyGame.Aseprite;

public class AsepriteTexture : IDisposable
{
	public Texture Texture;
	public Rectangle[] SourceRectangles;
	public AsepriteFile File;
	public string FilePath;

	public bool IsDisposed { get; private set; }

	public AsepriteTexture(Rectangle[] sourceRects, Texture texture, AsepriteFile asepriteFile, string filepath)
	{
		SourceRectangles = sourceRects;
		Texture = texture;
		File = asepriteFile;
		FilePath = filepath;
	}

	protected virtual void Dispose(bool isDisposing)
	{
		if (IsDisposed)
			return;
		
		if (isDisposing)
		{
			Texture.Dispose();
		}

		IsDisposed = true;
	}
	
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
