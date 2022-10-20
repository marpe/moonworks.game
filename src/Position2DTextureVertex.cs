namespace MyGame;

public struct Position2DTextureVertex
{
    public Vector2 Position;
    public Vector2 TexCoord;

    public Position2DTextureVertex(Vector2 position, Vector2 texcoord)
    {
        Position = position;
        TexCoord = texcoord;
    }
}

public struct Position3DTextureVertex
{
    public Vector3 Position;
    public Vector2 TexCoord;

    public Position3DTextureVertex(Vector3 position, Vector2 texcoord)
    {
        Position = position;
        TexCoord = texcoord;
    }
}
