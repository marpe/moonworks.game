namespace MyGame;

public readonly record struct GridCoords
{
    public readonly Vector2 CellPos;
    public readonly Point Cell;

    public GridCoords(Vector2 position)
    {
        Cell = ToCell(position);
        CellPos = ToRelativePositionInCell(position);
    }

    public static Point ToCell(Vector2 position, int gridSize = World.DefaultGridSize)
    {
        return new Point((int)(position.X / gridSize), (int)(position.Y / gridSize));
    }

    public static Vector2 ToRelativePositionInCell(Vector2 position, int gridSize = World.DefaultGridSize)
    {
        return new Vector2(position.X % gridSize / gridSize, position.Y % gridSize / gridSize);
    }
}
