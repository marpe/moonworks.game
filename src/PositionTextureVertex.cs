using System.Runtime.InteropServices;

namespace MyGame;

// SPIR-V requires vectors to not cross 16-byte boundaries
// [StructLayout(LayoutKind.Explicit)]
public struct PositionTextureColorVertex
{
    // [FieldOffset(0)]
    public Vector2 Position;
    // [FieldOffset(8)]
    public Vector2 TexCoord;
    // [FieldOffset(16)]
    public Color Color;
}

public struct Position3DTextureColorVertex
{
    public Vector3 Position;
    public Vector2 TexCoord;
    public Color Color;
}
