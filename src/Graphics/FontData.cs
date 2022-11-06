using MoonWorks.Graphics.Font;
using WellspringCS;

namespace MyGame.Graphics;

public class FontData
{
    private static byte[] _stringBytes = new byte[128];

    public FontType Name;

    public TextBatch Batch;
    public Packer Packer;
    public Font Font;
    public Texture? Texture;
    public bool HasStarted;

    public FontData(FontType name, TextBatch batch, Packer packer, Font font)
    {
        Name = name;
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

        Span<byte> byteSpan = _stringBytes.AsSpan();
        Encoding.UTF8.GetBytes(text, byteSpan);

        fixed (byte* bytes = byteSpan)
        {
            Wellspring.Wellspring_TextBounds(Packer.Handle, 0, 0, Wellspring.HorizontalAlignment.Left, Wellspring.VerticalAlignment.Top, (IntPtr)bytes, (uint)byteCount, out var rect);
            return new Vector2(rect.W, rect.H);
        }
    }

    public Vector2 MeasureString(char previousChar, char currentChar)
    {
        return MeasureString(stackalloc char[] { currentChar });
    }
}
