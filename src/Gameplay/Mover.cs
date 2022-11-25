namespace MyGame;

public struct CollisionResult
{
    public readonly ulong Frame;
    public readonly Point PreviousCell;
    public readonly Vector2 CellPos;
    public readonly Vector2 DeltaMove;
    public readonly Point CollisionCell;
    public readonly float ResultXyOnCollision;
    public CollisionDir Direction;

    public CollisionResult(CollisionDir direction, Point previousCell, Vector2 cellPos, float resultXYOnCollision, Vector2 deltaMove, Point collisionCell)
    {
        Direction = direction;
        PreviousCell = previousCell;
        CellPos = cellPos;
        DeltaMove = deltaMove;
        CollisionCell = collisionCell;
        ResultXyOnCollision = resultXYOnCollision;
        Frame = Shared.Game.Time.UpdateCount;
    }
}

public class Mover
{
    private static Point Up = new(0, -1);
    private static Point Right = new(1, 0);
    private static Point Down = new(0, 1);
    private static Point Left = new(-1, 0);
    private static Point UpRight = new(1, -1);
    private static Point UpLeft = new(-1, -1);
    private static Point DownRight = new(1, 1);
    private static Point DownLeft = new(-1, 1);

    private static Dictionary<Point, CollisionDir> _directionMap = new()
    {
        { Up, CollisionDir.Up },
        { Right, CollisionDir.Right },
        { Down, CollisionDir.Down },
        { Left, CollisionDir.Left },
        { UpRight, CollisionDir.UpRight },
        { UpLeft, CollisionDir.UpLeft },
        { DownRight, CollisionDir.DownRight },
        { DownLeft, CollisionDir.DownLeft },
    };

    private Entity? _parent;
    public Entity Parent => _parent ?? throw new InvalidOperationException();

    public Vector2 SizeInGridTiles => new(Parent.Size.X / (float)World.DefaultGridSize, Parent.Size.Y / (float)World.DefaultGridSize);

    public List<CollisionResult> PreviousMoveCollisions = new();
    public List<CollisionResult> MoveCollisions = new();
    public List<CollisionResult> ContinuedMoveCollisions = new();

    public List<CollisionResult> PreviousGroundCollisions = new();
    public List<CollisionResult> GroundCollisions = new();
    public List<CollisionResult> ContinuedGroundCollisions = new();

    private static readonly CollisionResult NoCollision = new();

    private Bounds Bounds => new(0, 0, 1 - SizeInGridTiles.X, 1 - SizeInGridTiles.Y);

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

        if (CheckCollisions(cell + Down))
            GroundCollisions.Add(new CollisionResult(CollisionDir.Down, cell, cellPos, 0, Vector2.Zero, cell + Down));
        if (cellPos.X > Bounds.Left && CheckCollisions(cell + DownRight))
            GroundCollisions.Add(new CollisionResult(CollisionDir.DownRight, cell, cellPos, 0, Vector2.Zero, cell + DownRight));
        if (cellPos.X < Bounds.Right && CheckCollisions(cell + DownLeft))
            GroundCollisions.Add(new CollisionResult(CollisionDir.DownLeft, cell, cellPos, 0, Vector2.Zero, cell + DownLeft));

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

    private bool CheckCollisions(Point cell) => Parent.Collider.HasCollision(cell);

    private bool CheckCollisionRight(Point cell, Vector2 nextCellPos, float xOnCollision, out CollisionResult result)
    {
        var cellDelta = nextCellPos.ToPoint();
        var didCollide = CheckCollisions(cell + cellDelta + Right) ||
                         (nextCellPos.Y < Bounds.Bottom && CheckCollisions(cell + cellDelta + UpRight)) ||
                         (nextCellPos.Y > Bounds.Top && CheckCollisions(cell + cellDelta + DownRight));

        result = NoCollision;
        if (didCollide)
        {
            var (_, prevCellPos) = Entity.GetGridCoords(Parent);
            result = new CollisionResult(CollisionDir.Right, cell, nextCellPos, xOnCollision, nextCellPos - prevCellPos, cell + nextCellPos.ToPoint());
        }

        return didCollide;
    }

    private bool CheckCollisionLeft(Point cell, Vector2 nextCellPos, float xOnCollision, out CollisionResult result)
    {
        var cellDelta = nextCellPos.ToPoint();

        var didCollide = CheckCollisions(cell + cellDelta + Left) ||
                         (nextCellPos.Y < Bounds.Bottom && CheckCollisions(cell + cellDelta + UpLeft)) ||
                         (nextCellPos.Y > Bounds.Top && CheckCollisions(cell + cellDelta + DownLeft));

        result = NoCollision;
        if (didCollide)
        {
            var (_, prevCellPos) = Entity.GetGridCoords(Parent);
            result = new CollisionResult(CollisionDir.Left, cell, nextCellPos, xOnCollision, nextCellPos - prevCellPos, cell + nextCellPos.ToPoint());
        }

        return didCollide;
    }

    private bool CheckCollisionDown(Point cell, Vector2 nextCellPos, float yOnCollision, out CollisionResult result)
    {
        var cellDelta = nextCellPos.ToPoint();

        var didCollide = CheckCollisions(cell + cellDelta + Down) ||
                         (nextCellPos.X > Bounds.Left && CheckCollisions(cell + cellDelta + DownRight)) ||
                         (nextCellPos.X < Bounds.Right && CheckCollisions(cell + cellDelta + DownLeft));

        result = NoCollision;
        if (didCollide)
        {
            var (_, prevCellPos) = Entity.GetGridCoords(Parent);
            result = new CollisionResult(CollisionDir.Down, cell, nextCellPos, yOnCollision, nextCellPos - prevCellPos, cell + nextCellPos.ToPoint());
        }

        return didCollide;
    }

    private bool CheckCollisionUp(Point cell, Vector2 nextCellPos, float yOnCollision, out CollisionResult result)
    {
        var cellDelta = nextCellPos.ToPoint();

        var didCollide = CheckCollisions(cell + cellDelta + Up) ||
                         (nextCellPos.X > Bounds.Left && CheckCollisions(cell + cellDelta + UpRight)) ||
                         (nextCellPos.X < Bounds.Right && CheckCollisions(cell + cellDelta + UpLeft));

        result = NoCollision;
        if (didCollide)
        {
            var (_, prevCellPos) = Entity.GetGridCoords(Parent);
            result = new CollisionResult(CollisionDir.Up, cell, nextCellPos, yOnCollision, nextCellPos - prevCellPos, cell + nextCellPos.ToPoint());
        }

        return didCollide;
    }

    public void PerformMove(Velocity velocity, float deltaSeconds)
    {
        PreviousMoveCollisions.Clear();
        PreviousMoveCollisions.AddRange(MoveCollisions);
        MoveCollisions.Clear();
        ContinuedMoveCollisions.Clear();

        if (velocity.Delta.LengthSquared() == 0)
            return;

        var result = NoCollision;

        // x-movement
        {
            var (cell, cellPos) = Entity.GetGridCoords(Parent);
            var nextCellPos = cellPos + velocity * Vector2.UnitX * deltaSeconds / World.DefaultGridSize;

            var didCollideX = nextCellPos.X > Bounds.Right && CheckCollisionRight(cell, nextCellPos, (int)nextCellPos.X + Bounds.Right, out result) ||
                              nextCellPos.X < Bounds.Left && CheckCollisionLeft(cell, nextCellPos, (int)nextCellPos.X + Bounds.Left, out result);

            if (didCollideX)
            {
                MoveCollisions.Add(result);
                Parent.Position.SetX((result.PreviousCell.X + result.ResultXyOnCollision) * World.DefaultGridSize);
                velocity.X = 0;
            }
            else
            {
                Parent.Position.DeltaMoveX(velocity.X * deltaSeconds);
            }
        }

        // y-movement
        {
            var (cell, cellPos) = Entity.GetGridCoords(Parent);
            var nextCellPos = cellPos + velocity * Vector2.UnitY * deltaSeconds / World.DefaultGridSize;

            var didCollideY = nextCellPos.Y > Bounds.Top && CheckCollisionDown(cell, nextCellPos, (int)nextCellPos.Y + Bounds.Top, out result) ||
                              nextCellPos.Y < Bounds.Bottom && CheckCollisionUp(cell, nextCellPos, (int)nextCellPos.Y + Bounds.Bottom, out result);

            if (didCollideY)
            {
                MoveCollisions.Add(result);
                Parent.Position.SetY((result.PreviousCell.Y + result.ResultXyOnCollision) * World.DefaultGridSize);
                velocity.Y = 0;
            }
            else
            {
                Parent.Position.DeltaMoveY(velocity.Y * deltaSeconds);
            }
        }

        var (finalCell, finalCellPos) = Entity.GetGridCoords(Parent);
        var hasCollision = Parent.Collider.HasCollision(finalCell.X, finalCell.Y);
        if (hasCollision && PreviousMoveCollisions.Count == 0)
        {
            Logger.LogInfo("Moved into collision tile!");
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
