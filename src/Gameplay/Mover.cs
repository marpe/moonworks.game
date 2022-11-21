namespace MyGame;

public class Mover
{
    private Entity? _parent;
    public Entity Parent => _parent ?? throw new InvalidOperationException();

    public void Initialize(Entity parent)
    {
        _parent = parent;
    }

    public bool IsGrounded(Vector2 velocity)
    {
        if (velocity.Y != 0)
            return false;

        var (cell, cellPos) = Entity.GetGridCoords(Parent);

        var size = new Vector2(0.4f, 0.8f);
        var halfSize = size * 0.5f;
        var maxX = (1.0f - halfSize.X);
        var minX = halfSize.X;

        var collisionBelow = Parent.Collider.HasCollision(cell.X, cell.Y + 1);

        if (cellPos.X < minX)
            collisionBelow |= Parent.Collider.HasCollision(cell.X - 1, cell.Y + 1);
        else if (cellPos.X > maxX)
            collisionBelow |= Parent.Collider.HasCollision(cell.X + 1, cell.Y + 1);

        return collisionBelow;
    }

    public CollisionDir PerformMove(Velocity velocity, float deltaSeconds)
    {
        if (velocity.Delta.LengthSquared() == 0)
            return CollisionDir.None;

        var result = CollisionDir.None;
        var size = new Vector2(0.4f, 0.8f);
        var halfSize = size * 0.5f;
        var maxX = (1.0f - halfSize.X);
        var minX = halfSize.X;

        if (velocity.X != 0)
        {
            var deltaMove = velocity * deltaSeconds / World.DefaultGridSize;
            var (cell, cellPos) = Entity.GetGridCoords(Parent);
            var dx = cellPos.X + deltaMove.X; // relative cell pos ( e.g < 0 means we moved to the previous cell )

            if (velocity.X > 0 && dx > maxX && Parent.Collider.HasCollision(cell.X + 1, cell.Y))
            {
                result |= CollisionDir.Right;
                Parent.Position.SetX((cell.X + maxX) * World.DefaultGridSize);
                velocity.X = 0;
            }
            else if (velocity.X < 0 && dx < minX && Parent.Collider.HasCollision(cell.X - 1, cell.Y))
            {
                result |= CollisionDir.Left;
                Parent.Position.SetX((cell.X + minX) * World.DefaultGridSize);
                velocity.X = 0;
            }
            else
            {
                Parent.Position.DeltaMoveX(velocity.X * deltaSeconds);
            }
        }

        if (velocity.Y != 0)
        {
            var deltaMove = velocity * deltaSeconds / World.DefaultGridSize;
            var (cell, cellPos) = Entity.GetGridCoords(Parent);
            var dy = cellPos.Y + deltaMove.Y; // relative cell pos ( e.g < 0 means we moved to the previous cell )

            var maxY = 1.0f;
            var minY = size.Y;

            var collisionBelow = Parent.Collider.HasCollision(cell.X, cell.Y + 1);
            var collisionAbove = Parent.Collider.HasCollision(cell.X, cell.Y - 1);
            
            if (cellPos.X < minX)
            {
                collisionBelow |= Parent.Collider.HasCollision(cell.X - 1, cell.Y + 1);
                collisionAbove |= Parent.Collider.HasCollision(cell.X - 1, cell.Y - 1);
            }
            else if (cellPos.X > maxX)
            {
                collisionBelow |= Parent.Collider.HasCollision(cell.X + 1, cell.Y + 1);
                collisionAbove |= Parent.Collider.HasCollision(cell.X + 1, cell.Y - 1);
            }

            if (velocity.Y > 0 && dy > maxY && collisionBelow)
            {
                result |= CollisionDir.Down;
                Parent.Position.SetY((cell.Y + maxY) * World.DefaultGridSize);
                velocity.Y = 0;
            }
            else if (velocity.Y < 0 && dy < minY && collisionAbove)
            {
                result |= CollisionDir.Top;
                Parent.Position.SetY((cell.Y + minY) * World.DefaultGridSize);
                velocity.Y = 0;
            }
            else
            {
                Parent.Position.DeltaMoveY(velocity.Y * deltaSeconds);
            }
        }

        return result;
    }
}
