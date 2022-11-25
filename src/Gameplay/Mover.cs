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

    public void Update()
    {
        while (CellPos.X > 1f)
        {
            CellPos.X--;
            Cell.X++;
        }

        while (CellPos.X < 0)
        {
            CellPos.X++;
            Cell.X--;
        }

        while (CellPos.Y > 1f)
        {
            CellPos.Y--;
            Cell.Y++;
        }

        while (CellPos.Y < 0)
        {
            CellPos.Y++;
            Cell.Y--;
        }
    }

    public string DebugDisplayString => $"c: {Cell.X}, {Cell.Y} r: {StringExt.TruncateNumber(CellPos.X)}, {StringExt.TruncateNumber(CellPos.Y)}";
}

public struct CollisionResult
{
    public CollisionDir Direction;
    public GridCoords Previous;
    public GridCoords Next;

    public CollisionResult(CollisionDir direction, GridCoords previous, GridCoords next)
    {
        Direction = direction;
        Previous = previous;
        Next = next;
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

        var gridCoords = Parent.GridCoords;
        if (CheckCollisions(gridCoords.Cell + Down))
            GroundCollisions.Add(new CollisionResult(CollisionDir.Down, gridCoords, gridCoords));
        if (gridCoords.CellPos.X > Bounds.Right && CheckCollisions(gridCoords.Cell + DownRight))
            GroundCollisions.Add(new CollisionResult(CollisionDir.DownRight, gridCoords, gridCoords));

        for (var i = 0; i < PreviousGroundCollisions.Count; i++)
        {
            var prev = PreviousGroundCollisions[i];
            for (var j = 0; j < GroundCollisions.Count; j++)
            {
                var curr = GroundCollisions[j];
                if (prev.Next.Cell == curr.Next.Cell)
                {
                    ContinuedGroundCollisions.Add(prev);
                    break;
                }
            }
        }

        return GroundCollisions.Count > 0;
    }

    private bool CheckCollisions(in Point cell) => Parent.Collider.HasCollision(cell);

    private void SanityCheck(Vector2 deltaMove)
    {
        // -----------------------------------------------
        var (finalCell, finalCellPos) = Entity.GetGridCoords(Parent, deltaMove);
        var hasCollision = Parent.Collider.HasCollision(finalCell.X, finalCell.Y);
        if (hasCollision && PreviousMoveCollisions.Count == 0)
            Logger.LogInfo("Moved into collision tile!");
        // -----------------------------------------------
    }

    private void HandleHorizontalMovement(in GridCoords prevGridCoords, ref GridCoords gridCoords, ref Vector2 deltaMove)
    {
        if (gridCoords.CellPos.X > Bounds.Right)
        {
            if (CheckCollisions(gridCoords.Cell + Right))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Right, prevGridCoords, gridCoords));
                gridCoords.CellPos.X = Bounds.Right;
                deltaMove.X = 0;
                Logger.LogInfo("Collided right");
            }
            else if (gridCoords.CellPos.Y > Bounds.Bottom && CheckCollisions(gridCoords.Cell + DownRight))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Right, prevGridCoords, gridCoords));
                gridCoords.CellPos.X = Bounds.Right;
                deltaMove.X = 0;
                Logger.LogInfo("Collided right down");
            }
        }

        if (gridCoords.CellPos.X < Bounds.Left)
        {
            if (CheckCollisions(gridCoords.Cell + Left))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Left, prevGridCoords, gridCoords));
                gridCoords.CellPos.X = Bounds.Left;
                deltaMove.X = 0;
                Logger.LogInfo("Collided left");
            }
            else if (gridCoords.CellPos.Y > Bounds.Bottom && CheckCollisions(gridCoords.Cell + DownLeft))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Left, prevGridCoords, gridCoords));
                gridCoords.CellPos.X = Bounds.Left;
                deltaMove.X = 0;
                Logger.LogInfo("Collided left down");
            }
        }
    }

    private void HandleVerticalMovement(in GridCoords prevGridCoords, ref GridCoords gridCoords, ref Vector2 deltaMove)
    {
        if (gridCoords.CellPos.Y < Bounds.Top)
        {
            if (CheckCollisions(gridCoords.Cell + Up))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Up, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y = Bounds.Top;
                deltaMove.Y = 0;
            }
            else if ((gridCoords.CellPos.X > Bounds.Right && CheckCollisions(gridCoords.Cell + UpRight)))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Up, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y = Bounds.Top;
                deltaMove.Y = 0;
            }
        }


        if (gridCoords.CellPos.Y > Bounds.Bottom)
        {
            if (CheckCollisions(gridCoords.Cell + Down))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Down, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y = Bounds.Bottom;
                deltaMove.Y = 0;
                Logger.LogInfo("Collided down");
            }
            else
            {
                var hasCollision = CheckCollisions(gridCoords.Cell + DownRight);
                if (deltaMove.Y > 0 && gridCoords.CellPos.X > Bounds.Right && prevGridCoords.CellPos.Y <= Bounds.Bottom && hasCollision)
                {
                    MoveCollisions.Add(new CollisionResult(CollisionDir.Down, prevGridCoords, gridCoords));
                    gridCoords.CellPos.Y = Bounds.Bottom;
                    deltaMove.Y = 0;
                    Logger.LogInfo("Collided down right");
                }
            }
        }
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
        var steps = MathF.CeilToInt((MathF.Abs(deltaMove.X) + MathF.Abs(deltaMove.Y)) / 0.33f);
        var gridCoords = Parent.GridCoords;

        if (steps > 0)
        {
            var n = 0;
            while (n < steps)
            {
                var preUpdateCoords = gridCoords;
                gridCoords.CellPos.X += deltaMove.X / steps;
                // var nextGridCoords = gridCoords;
                // nextGridCoords.Update();

                if (deltaMove.X != 0)
                {
                    HandleHorizontalMovement(preUpdateCoords, ref gridCoords, ref deltaMove);
                }

                gridCoords.Update();
                preUpdateCoords = gridCoords;
                gridCoords.CellPos.Y += deltaMove.Y / steps;
                // nextGridCoords = gridCoords;
                // nextGridCoords.Update();

                if (deltaMove.Y != 0)
                {
                    HandleVerticalMovement(preUpdateCoords, ref gridCoords, ref deltaMove);
                }

                gridCoords.Update();

                n++;
            }

            var finalPosition = (gridCoords.Cell + gridCoords.CellPos) * World.DefaultGridSize;
            Parent.Position.Set(finalPosition);
        }

        foreach (var collision in MoveCollisions)
        {
            if (collision.Direction == CollisionDir.Right)
                velocity.X = 0;
            else if (collision.Direction == CollisionDir.Left)
                velocity.X = 0;
            else if (collision.Direction == CollisionDir.Up)
                velocity.Y = 0;
            else if (collision.Direction == CollisionDir.Down)
                velocity.Y = 0;
        }

        Velocity.ApplyFriction(velocity);

        for (var i = 0; i < PreviousMoveCollisions.Count; i++)
        {
            var prev = PreviousMoveCollisions[i];
            for (var j = 0; j < MoveCollisions.Count; j++)
            {
                var curr = MoveCollisions[j];
                if (prev.Next.Cell == curr.Next.Cell)
                {
                    ContinuedMoveCollisions.Add(prev);
                    break;
                }
            }
        }
    }
}
