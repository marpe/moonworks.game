using System.Runtime.InteropServices;

namespace MyGame;

public struct Position3DTextureColorVertex
{
    public Vector3 Position;
    public Vector2 TexCoord;
    public Vector4 Color;

    public Position3DTextureColorVertex(Vector3 position, Vector2 texcoord, Color color)
    {
        Position = position;
        TexCoord = texcoord;
        Color = new Vector4(
            color.R / 255f,
            color.G / 255f,
            color.B / 255f,
            color.A / 255f
        );
    }
}

// SPIR-V requires vectors to not cross 16-byte boundaries
[StructLayout(LayoutKind.Explicit)]
public struct VertexPositionTexcoord
{
    [FieldOffset(0)] public Vector2 position;
    [FieldOffset(8)] public Vector2 texcoord;
    [FieldOffset(16)] public Color color;
}
