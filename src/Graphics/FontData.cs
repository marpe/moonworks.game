namespace MyGame.Graphics;

public class TTFFont
{
    public Dictionary<int, FontData> Sizes = new();

    public void Add(int size, FontData fontData)
    {
        Sizes.Add(size, fontData);
    }
}

public class FontData
{
    public int Size { get; }
    private static byte[] _stringBytes = new byte[128];

    public TextBatch Batch;
    public Font Font;
    public bool HasStarted;

    public Packer Packer;
    public Texture? Texture;

    public FontData(TextBatch batch, Packer packer, Font font, int size)
    {
        Size = size;
        Batch = batch;
        Packer = packer;
        Font = font;
    }

    public unsafe Vector2 MeasureString(ReadOnlySpan<char> text)
    {
        var byteCount = Encoding.UTF8.GetByteCount(text);

        if (_stringBytes.Length < byteCount)
        {
            Array.Resize(ref _stringBytes, byteCount);
        }

        var byteSpan = _stringBytes.AsSpan();
        Encoding.UTF8.GetBytes(text, byteSpan);

        fixed (byte* bytes = byteSpan)
        {
            Wellspring.Wellspring_TextBounds(Packer.Handle, 0, 0, Wellspring.HorizontalAlignment.Left, Wellspring.VerticalAlignment.Top, (IntPtr)bytes,
                (uint)byteCount, out var rect);
            return new Vector2(rect.W, rect.H);
        }
    }

    public Vector2 MeasureString(char previousChar, char currentChar)
    {
        return MeasureString(stackalloc char[] { currentChar });
    }
}
