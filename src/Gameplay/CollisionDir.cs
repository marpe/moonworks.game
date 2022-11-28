namespace MyGame;

[Flags]
public enum CollisionDir
{
    None = 0,
    Up = 1 << 0,
    Right = 1 << 1,
    Down = 1 << 2,
    Left = 1 << 3,
    UpRight = 1 << 4,
    UpLeft = 1 << 5,
    DownRight = 1 << 6,
    DownLeft = 1 << 7,
}
