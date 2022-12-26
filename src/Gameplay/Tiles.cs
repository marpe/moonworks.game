namespace MyGame;

public static class LayerDefs
{
    public enum Tiles : long
    {
        Mushroom = 1,
        Flower = 2,
        Grass = 8,
        Fence = 9,
        Tree = 10,
        Ground = 12,
        Left_Ground = 7,
    }

    public static Dictionary<Tiles, Color> TilesColors = new()
    {
        { Tiles.Mushroom, new Color(196f, 6f, 6f, 255f) },
        { Tiles.Flower, new Color(201f, 68f, 68f, 255f) },
        { Tiles.Grass, new Color(55f, 255f, 124f, 255f) },
        { Tiles.Fence, new Color(149f, 106f, 0f, 255f) },
        { Tiles.Tree, new Color(2f, 127f, 66f, 255f) },
        { Tiles.Ground, new Color(57f, 68f, 214f, 255f) },
        { Tiles.Left_Ground, new Color(188f, 38f, 200f, 255f) },
    };
}
