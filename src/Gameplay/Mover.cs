namespace MyGame;

[DebuggerDisplay("{DebugDisplayString,nq}")]
public struct GridCoords
{
    public Vector2 CellPos;
    public Point Cell;

    public GridCoords(Point cell, Vector2 cellPos)
    {
        Cell = cell;
        CellPos = cellPos;
    }

    public string DebugDisplayString => $"c: {Cell.X}, {Cell.Y} r: {StringExt.TruncateNumber(CellPos.X)}, {StringExt.TruncateNumber(CellPos.Y)}";
}

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
        if (cellPos.X > Bounds.Right && CheckCollisions(cell + DownRight))
            GroundCollisions.Add(new CollisionResult(CollisionDir.DownRight, cell, cellPos, 0, Vector2.Zero, cell + DownRight));

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

    private bool CheckCollisionRight(in GridCoords gridCoords, out CollisionResult result)
    {
        var cellDelta = nextCellPos.ToPoint();
        var didCollide = CheckCollisions(cell + Right) ||
                         (nextCellPos.Y > Bounds.Bottom && CheckCollisions(cell + cellDelta + DownRight));

        result = NoCollision;
        if (didCollide)
        {
            var (_, prevCellPos) = Entity.GetGridCoords(Parent);
            result = new CollisionResult(CollisionDir.Right, cell, nextCellPos, xOnCollision, nextCellPos - prevCellPos, cell + nextCellPos.ToPoint());
        }

        return didCollide;
    }

    private bool CheckCollisionLeft(in GridCoords gridCoords, out CollisionResult result)
    {
        var cellDelta = nextCellPos.ToPoint();

        var didCollide = CheckCollisions(cell + cellDelta + Left) ||
                         (nextCellPos.Y > Bounds.Bottom && CheckCollisions(cell + cellDelta + DownLeft));

        result = NoCollision;
        if (didCollide)
        {
            var (_, prevCellPos) = Entity.GetGridCoords(Parent);
            result = new CollisionResult(CollisionDir.Left, cell, nextCellPos, xOnCollision, nextCellPos - prevCellPos, cell + nextCellPos.ToPoint());
        }

        return didCollide;
    }

    private bool CheckCollisionDown(in GridCoords gridCoords, out CollisionResult result)
    {
        var cellDelta = nextCellPos.ToPoint();

        var didCollide = CheckCollisions(cell + Down) ||
                         (nextCellPos.X > Bounds.Right && CheckCollisions(cell + cellDelta + DownRight));

        result = NoCollision;
        if (didCollide)
        {
            var (_, prevCellPos) = Entity.GetGridCoords(Parent);
            result = new CollisionResult(CollisionDir.Down, cell, nextCellPos, yOnCollision, nextCellPos - prevCellPos, cell + nextCellPos.ToPoint());
        }

        return didCollide;
    }

    private bool CheckCollisionUp(in GridCoords gridCoords, out CollisionResult result)
    {
        var cellDelta = nextCellPos.ToPoint();

        var didCollide = CheckCollisions(cell + cellDelta + Up) ||
                         (nextCellPos.X > Bounds.Right && CheckCollisions(cell + cellDelta + UpRight));

        result = NoCollision;
        if (didCollide)
        {
            var (_, prevCellPos) = Entity.GetGridCoords(Parent);
            result = new CollisionResult(CollisionDir.Up, cell, nextCellPos, yOnCollision, nextCellPos - prevCellPos, cell + nextCellPos.ToPoint());
        }

        return didCollide;
    }

    private void SanityCheck(Vector2 deltaMove)
    {
        // -----------------------------------------------
        var (finalCell, finalCellPos) = Entity.GetGridCoords(Parent, deltaMove);
        var hasCollision = Parent.Collider.HasCollision(finalCell.X, finalCell.Y);
        if (hasCollision && PreviousMoveCollisions.Count == 0)
            Logger.LogInfo("Moved into collision tile!");
        // -----------------------------------------------
    }


    private void HandleHorizontalMovement(ref GridCoords gridCoords)
    {
        if (gridCoords.CellPos.X > Bounds.Right && CheckCollisionRight(gridCoords))
        {
            // (int)nextCellPos.X + Bounds.Right
            MoveCollisions.Add(result);
            Parent.Position.SetX((result.PreviousCell.X + result.ResultXyOnCollision) * World.DefaultGridSize);
            velocity.X = 0;
        }

        if (gridCoords.Cell.X < Bounds.Left && CheckCollisionLeft(gridCoords))
        {
        }

        Parent.Position.DeltaMoveX(velocity.X * deltaSeconds);
    }

    private void HandleVerticalMovement(ref GridCoords gridCoords)
    {
        if (gridCoords.CellPos.X > Bounds.Right && CheckCollisionRight(gridCoords))
        {
            //nextCellPos.Y > Bounds.Bottom &&
            // (int)nextCellPos.Y + Bounds.Bottom

            MoveCollisions.Add(result);
            Parent.Position.SetY((result.PreviousCell.Y + result.ResultXyOnCollision) * World.DefaultGridSize);
            velocity.Y = 0;
        }

        if (gridCoords.CellPos.Y < Bounds.Top && CheckCollisionTop(gridCoords))
        {
            // nextCellPos.Y < Bounds.Top
            // (int)nextCellPos.Y + Bounds.Top
        }

        Parent.Position.DeltaMoveY(velocity.Y * deltaSeconds);
    }


    public void PerformMove(Velocity velocity, float deltaSeconds)
    {
        PreviousMoveCollisions.Clear();
        PreviousMoveCollisions.AddRange(MoveCollisions);
        MoveCollisions.Clear();
        ContinuedMoveCollisions.Clear();

        if (velocity.Delta.LengthSquared() == 0)
            return;

        var deltaMove = velocity * deltaSeconds / World.DefaultGridSize;
        var steps = MathF.CeilToInt(MathF.Abs(deltaMove.X) + MathF.Abs(deltaMove.Y)) / 0.33f;
        var gridCoords = Parent.GridCoords;

        if (steps > 0)
        {
            var n = 0;
            while (n < steps)
            {
                gridCoords.CellPos.X += deltaMove.X / steps;

                if (deltaMove.X != 0)
                {
                    HandleHorizontalMovement(ref gridCoords);
                }

                while (gridCoords.CellPos.X >= 1f)
                {
                    gridCoords.CellPos.X--;
                    gridCoords.Cell.X++;
                }

                while (gridCoords.CellPos.X < 0)
                {
                    gridCoords.CellPos.X++;
                    gridCoords.Cell.X--;
                }

                gridCoords.CellPos.Y += deltaMove.Y / steps;

                if (deltaMove.Y != 0)
                {
                    HandleVerticalMovement(ref gridCoords);
                }

                while (gridCoords.CellPos.Y > 1f)
                {
                    gridCoords.CellPos.Y--;
                    gridCoords.Cell.Y++;
                }

                while (gridCoords.CellPos.Y < 0)
                {
                    gridCoords.CellPos.Y++;
                    gridCoords.Cell.Y--;
                }

                n++;
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
