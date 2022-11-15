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
        var (cell, _) = Entity.GetGridCoords(Parent);
        return velocity.Y == 0 && Parent.Collider.HasCollision(cell.X, cell.Y + 1);
    }

    public CollisionDir PerformMove(Velocity velocity, float deltaSeconds)
    {
        if (velocity.Delta.LengthSquared() == 0)
            return CollisionDir.None;

        var result = CollisionDir.None;
        var size = new Vector2(0.4f, 0.8f);
        var halfSize = size * 0.5f;

        if (velocity.X != 0)
        {
            var deltaMove = velocity * deltaSeconds / World.DefaultGridSize;
            var (cell, cellPos) = Entity.GetGridCoords(Parent);
            var dx = cellPos.X + deltaMove.X; // relative cell pos ( e.g < 0 means we moved to the previous cell )

            var maxX = (1.0f - halfSize.X);
            var minX = halfSize.X;

            if (velocity.X > 0 && dx > maxX && Parent.Collider.HasCollision(cell.X + 1, cell.Y))
            {
                result |= CollisionDir.Right;
                Parent.Position.X = (cell.X + maxX) * World.DefaultGridSize;
                velocity.X = 0;
            }
            else if (velocity.X < 0 && dx < minX && Parent.Collider.HasCollision(cell.X - 1, cell.Y))
            {
                result |= CollisionDir.Left;
                Parent.Position.X = (cell.X + minX) * World.DefaultGridSize;
                velocity.X = 0;
            }
            else
            {
                Parent.Position.X += velocity.X * deltaSeconds;
            }

            (Parent.Cell, Parent.CellPos) = Entity.GetGridCoords(Parent);
        }

        if (velocity.Y != 0)
        {
            var deltaMove = velocity * deltaSeconds / World.DefaultGridSize;
            var (cell, cellPos) = Entity.GetGridCoords(Parent);
            var dy = cellPos.Y + deltaMove.Y; // relative cell pos ( e.g < 0 means we moved to the previous cell )

            var maxY = 1.0f;
            var minY = size.Y;

            if (velocity.Y > 0 && dy > maxY && Parent.Collider.HasCollision(cell.X, cell.Y + 1))
            {
                result |= CollisionDir.Down;
                Parent.Position.Y = (cell.Y + maxY) * World.DefaultGridSize;
                velocity.Y = 0;
            }
            else if (velocity.Y < 0 && dy < minY && Parent.Collider.HasCollision(cell.X, cell.Y - 1))
            {
                result |= CollisionDir.Top;
                Parent.Position.Y = (cell.Y + minY) * World.DefaultGridSize;
                velocity.Y = 0;
            }
            else
            {
                Parent.Position.Y += velocity.Y * deltaSeconds;
            }

            (Parent.Cell, Parent.CellPos) = Entity.GetGridCoords(Parent);
        }

        return result;
    }
}
