namespace MyGame.Utils;

public struct DisplayMode
{
    public Point Size;
    public int RefreshRate;
    public int X => Size.X;
    public int Y => Size.Y;

    public DisplayMode(int x, int y, int refreshRate)
    {
        Size = new Point(x, y);
        RefreshRate = refreshRate;
    }
}
