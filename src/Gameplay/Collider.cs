namespace MyGame;

[Flags]
public enum CollisionDir
{
    None = 0,
    Top = 1 << 0,
    Right = 1 << 1,
    Down = 1 << 2,
    Left = 1 << 3,
}

public class Collider
{
    private Entity? _parent;
    public Entity Parent => _parent ?? throw new InvalidOperationException();

    public void Initialize(Entity parent)
    {
        _parent = parent;
    }

    public bool HasCollision(int x, int y)
    {
        return Parent.World.HasCollision(x, y);
    }
}
