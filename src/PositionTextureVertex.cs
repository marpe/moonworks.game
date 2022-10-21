namespace MyGame;

public struct PositionTextureColorVertex
{
    public Vector2 Position;
    public Vector2 TexCoord;
    public Vector4 Color;

    public PositionTextureColorVertex(Vector2 position, Vector2 texcoord, Color color)
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
