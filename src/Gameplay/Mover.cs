namespace MyGame;

public struct CollisionResult
{
    public readonly ulong Frame;
    public readonly Point Cell;
    public readonly CollisionDir Direction;
    public readonly float CellPos;
    public readonly Vector2 VelocityDelta;
    public readonly Point CollisionCell;
    public readonly float Depth;

    public CollisionResult(CollisionDir direction, Point cell, float cellPos, float depth, Vector2 velocityDelta, Point collisionCell)
    {
        Direction = direction;
        Cell = cell;
        CellPos = cellPos;
        VelocityDelta = velocityDelta;
        CollisionCell = collisionCell;
        Depth = depth;
        Frame = Shared.Game.Time.UpdateCount;
    }
}

public class Mover
{
    private Entity? _parent;
    public Entity Parent => _parent ?? throw new InvalidOperationException();

    public Point PixelSize = new(8, 14);
    public Vector2 Size => new((float)PixelSize.X / World.DefaultGridSize, (float)PixelSize.Y / World.DefaultGridSize);

    public List<CollisionResult> PreviousMoveCollisions = new();
    public List<CollisionResult> MoveCollisions = new();
    public List<CollisionResult> ContinuedMoveCollisions = new();

    public List<CollisionResult> PreviousGroundCollisions = new();
    public List<CollisionResult> GroundCollisions = new();
    public List<CollisionResult> ContinuedGroundCollisions = new();

    public void Initialize(Entity parent)
    {
        _parent = parent;
    }

    public bool IsGrounded(Velocity velocity)
    {
        PreviousGroundCollisions.Clear();
        PreviousGroundCollisions.AddRange(GroundCollisions);
        GroundCollisions.Clear();
        ContinuedGroundCollisions.Clear();

        if (velocity.Y != 0)
            return false;
        
        var (cell, cellPos) = Entity.GetGridCoords(Parent);

        var halfSize = Size * 0.5f;
        var maxX = (1.0f - halfSize.X);
        var minX = halfSize.X;

        if (Parent.Collider.HasCollision(cell.X, cell.Y + 1))
            GroundCollisions.Add(new CollisionResult(CollisionDir.Down, cell, cellPos.X, 0, velocity.Delta, new Point(cell.X, cell.Y + 1)));
        else if (cellPos.X < minX && Parent.Collider.HasCollision(cell.X - 1, cell.Y + 1))
            GroundCollisions.Add(new CollisionResult(CollisionDir.Down | CollisionDir.Left, cell, cellPos.X, 0, velocity.Delta, new Point(cell.X - 1, cell.Y + 1)));
        else if (cellPos.X > maxX && Parent.Collider.HasCollision(cell.X + 1, cell.Y + 1))
            GroundCollisions.Add(new CollisionResult(CollisionDir.Down | CollisionDir.Right, cell, cellPos.X, 0, velocity.Delta, new Point(cell.X + 1, cell.Y + 1)));

        for (var i = 0; i < PreviousGroundCollisions.Count; i++)
        {
            var prev = PreviousGroundCollisions[i];
            for (var j = 0; j < GroundCollisions.Count; j++)
            {
                var curr = GroundCollisions[j];
                if (prev.CollisionCell == curr.CollisionCell)
                {
                    ContinuedGroundCollisions.Add(prev);
                    break;
                }
            }
        }
        
        return GroundCollisions.Count > 0;
    }

    public void PerformMove(Velocity velocity, float deltaSeconds)
    {
        PreviousMoveCollisions.Clear();
        PreviousMoveCollisions.AddRange(MoveCollisions);
        MoveCollisions.Clear();
        ContinuedMoveCollisions.Clear();

        if (velocity.Delta.LengthSquared() == 0)
            return;

        var halfSize = Size * 0.5f;
        var maxX = (1.0f - halfSize.X);
        var minX = halfSize.X;

        if (velocity.X != 0)
        {
            var deltaMove = velocity * deltaSeconds / World.DefaultGridSize;
            var (cell, cellPos) = Entity.GetGridCoords(Parent);
            var dx = cellPos.X + deltaMove.X; // relative cell pos ( e.g < 0 means we moved to the previous cell )

            var rightCell = new Point(cell.X + 1, cell.Y);
            var leftCell = new Point(cell.X - 1, cell.Y);
            if (velocity.X > 0 && dx > maxX && Parent.Collider.HasCollision(rightCell))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Right, cell, dx, maxX - dx, velocity.Delta, rightCell));
                velocity.X = 0;
                Parent.Position.SetX((cell.X + maxX) * World.DefaultGridSize);
            }
            else if (velocity.X < 0 && dx < minX && Parent.Collider.HasCollision(leftCell))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Left, cell, dx, minX - dx, velocity.Delta, leftCell));
                velocity.X = 0;
                Parent.Position.SetX((cell.X + minX) * World.DefaultGridSize);
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
            var minY = Size.Y;

            // collisions below
            if (velocity.Y > 0 && dy > maxY && Parent.Collider.HasCollision(cell.X, cell.Y + 1))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Down, cell, dy, maxY - dy, velocity.Delta, new Point(cell.X, cell.Y + 1)));
                velocity.Y = 0;
                Parent.Position.SetY((cell.Y + maxY) * World.DefaultGridSize);
            }
            else if (cellPos.X < minX && velocity.Y > 0 && dy > maxY && Parent.Collider.HasCollision(cell.X - 1, cell.Y + 1))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Down | CollisionDir.Left, cell, dy, maxY - dy, velocity.Delta, new Point(cell.X - 1, cell.Y + 1)));
                velocity.Y = 0;
                Parent.Position.SetY((cell.Y + maxY) * World.DefaultGridSize);
            }
            else if (cellPos.X > maxX && velocity.Y > 0 && dy > maxY && Parent.Collider.HasCollision(cell.X + 1, cell.Y + 1))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Down | CollisionDir.Right, cell, dy, maxY - dy, velocity.Delta, new Point(cell.X + 1, cell.Y + 1)));
                velocity.Y = 0;
                Parent.Position.SetY((cell.Y + maxY) * World.DefaultGridSize);
            }
            // collisions above
            else if (velocity.Y < 0 && dy < minY && Parent.Collider.HasCollision(cell.X, cell.Y - 1))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Top, cell, dy, minY - dy, velocity.Delta, new Point(cell.X, cell.Y + 1)));
                velocity.Y = 0;
                Parent.Position.SetY((cell.Y + minY) * World.DefaultGridSize);
            }
            else if (cellPos.X < minX && velocity.Y < 0 && dy < minY && Parent.Collider.HasCollision(cell.X - 1, cell.Y - 1))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Top | CollisionDir.Left, cell, dy, minY - dy, velocity.Delta, new Point(cell.X - 1, cell.Y + 1)));
                velocity.Y = 0;
                Parent.Position.SetY((cell.Y + minY) * World.DefaultGridSize);
            }
            else if (cellPos.X > maxX && velocity.Y < 0 && dy < minY && Parent.Collider.HasCollision(cell.X + 1, cell.Y - 1))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Top | CollisionDir.Right, cell, dy, minY - dy, velocity.Delta, new Point(cell.X + 1, cell.Y + 1)));
                velocity.Y = 0;
                Parent.Position.SetY((cell.Y + minY) * World.DefaultGridSize);
            }
            else
            {
                Parent.Position.DeltaMoveY(velocity.Y * deltaSeconds);
            }
        }

        for (var i = 0; i < PreviousMoveCollisions.Count; i++)
        {
            var prev = PreviousMoveCollisions[i];
            for (var j = 0; j < MoveCollisions.Count; j++)
            {
                var curr = MoveCollisions[j];
                if (prev.CollisionCell == curr.CollisionCell)
                {
                    ContinuedMoveCollisions.Add(prev);
                    break;
                }
            }
        }
    }
}
