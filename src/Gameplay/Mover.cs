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

    private Point CeilSize => new(MathF.CeilToInt(SizeInGridTiles.X), MathF.CeilToInt(SizeInGridTiles.Y));

    public void Initialize(Entity parent)
    {
        _parent = parent;
    }

    public static bool HasCollisionInDirection(CollisionDir dir, List<CollisionResult> collisions)
    {
        foreach (var collision in collisions)
        {
            if (collision.Direction == dir)
                return true;
        }

        return false;
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
        if (gridCoords.CellPos.Y + SizeInGridTiles.Y < CeilSize.Y)
            return false;

        if (HasCollision(gridCoords.Cell, 0, CeilSize.Y))
            GroundCollisions.Add(new CollisionResult(CollisionDir.Down, gridCoords, gridCoords));
        if (gridCoords.CellPos.X + SizeInGridTiles.X > CeilSize.X &&
            Parent.Collider.HasCollision(gridCoords.Cell.X + CeilSize.X, gridCoords.Cell.Y + CeilSize.Y))
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

    private void HandleHorizontalMovement(in GridCoords prevGridCoords, ref GridCoords gridCoords, ref Vector2 deltaMove)
    {
        if (gridCoords.CellPos.X + SizeInGridTiles.X > CeilSize.X)
        {
            if (HasCollision(gridCoords.Cell, CeilSize.X, 0))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Right, prevGridCoords, gridCoords));
                gridCoords.CellPos.X -= gridCoords.CellPos.X + SizeInGridTiles.X - CeilSize.X;
                deltaMove.X = 0;
                Logger.LogInfo("Collided right");
            }
            else if (gridCoords.CellPos.Y + SizeInGridTiles.Y > CeilSize.Y &&
                     Parent.Collider.HasCollision(gridCoords.Cell.X + CeilSize.X, gridCoords.Cell.Y + CeilSize.Y))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Right, prevGridCoords, gridCoords));
                gridCoords.CellPos.X -= gridCoords.CellPos.X + SizeInGridTiles.X - CeilSize.X;
                deltaMove.X = 0;
                Logger.LogInfo("Collided right down");
            }
        }

        if (gridCoords.CellPos.X < 0)
        {
            if (HasCollision(gridCoords.Cell, -1, 0))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Left, prevGridCoords, gridCoords));
                gridCoords.CellPos.X = 0;
                deltaMove.X = 0;
                Logger.LogInfo("Collided left");
            }
            else if (gridCoords.CellPos.Y + SizeInGridTiles.Y > CeilSize.Y &&
                     Parent.Collider.HasCollision(gridCoords.Cell.X + Left.X, gridCoords.Cell.Y + CeilSize.Y))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Left, prevGridCoords, gridCoords));
                gridCoords.CellPos.X = 0;
                deltaMove.X = 0;
                Logger.LogInfo("Collided left down");
            }
        }
    }

    private bool HasCollision(Point topLeft, int dx, int dy)
    {
        for (var x = 0; x < CeilSize.X; x++)
        {
            if (Parent.Collider.Parent.Collider.HasCollision(topLeft.X + x, topLeft.Y + dy))
                return true;
        }


        for (var y = 0; y < CeilSize.Y; y++)
        {
            if (Parent.Collider.Parent.Collider.HasCollision(topLeft.X + dx, topLeft.Y + y))
                return true;
        }

        return false;
    }

    private void HandleVerticalMovement(in GridCoords prevGridCoords, ref GridCoords gridCoords, ref Vector2 deltaMove)
    {
        var newGridCoords = gridCoords;
        newGridCoords.Update();

        if (newGridCoords.Cell.Y < gridCoords.Cell.Y)
        {
            if (HasCollision(newGridCoords.Cell, 0, 0))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Up, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y = 0;
                deltaMove.Y = 0;
                Logger.LogInfo("Collided up");
            }
            else if (gridCoords.CellPos.X + SizeInGridTiles.X > CeilSize.X &&
                     Parent.Collider.HasCollision(newGridCoords.Cell.X + CeilSize.X, newGridCoords.Cell.Y))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Up, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y = 0;
                deltaMove.Y = 0;
                Logger.LogInfo("Collided up right");
            }
        }
        // new cell collision down
        // new cell collision down right
        // same cell, collision down
        // same cell collision down right

        if (newGridCoords.Cell.Y > gridCoords.Cell.Y)
        {
            if (newGridCoords.CellPos.Y + SizeInGridTiles.Y > CeilSize.Y && HasCollision(newGridCoords.Cell, 0, CeilSize.Y))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Down, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y -= gridCoords.CellPos.Y + SizeInGridTiles.Y - CeilSize.Y;
                deltaMove.Y = 0;
                Logger.LogInfo("Collided down 1");
            }
            else if (newGridCoords.CellPos.Y + SizeInGridTiles.Y > CeilSize.Y &&
                     gridCoords.CellPos.X + SizeInGridTiles.X > CeilSize.X &&
                     Parent.Collider.HasCollision(gridCoords.Cell.X + CeilSize.X, newGridCoords.Cell.Y + CeilSize.Y))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Down, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y -= gridCoords.CellPos.Y + SizeInGridTiles.Y - CeilSize.Y;
                deltaMove.Y = 0;
                Logger.LogInfo("Collided down right 1");
            }
        }
        else
        {
            if (gridCoords.CellPos.Y + SizeInGridTiles.Y > CeilSize.Y && HasCollision(gridCoords.Cell, 0, CeilSize.Y))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Down, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y -= gridCoords.CellPos.Y + SizeInGridTiles.Y - CeilSize.Y;
                deltaMove.Y = 0;
                Logger.LogInfo("Collided down 2");
            }
            else if (gridCoords.CellPos.Y + SizeInGridTiles.Y > CeilSize.Y &&
                     gridCoords.CellPos.X + SizeInGridTiles.X > CeilSize.X &&
                     Parent.Collider.HasCollision(gridCoords.Cell.X + CeilSize.X, gridCoords.Cell.Y + CeilSize.Y))
            {
                MoveCollisions.Add(new CollisionResult(CollisionDir.Down, prevGridCoords, gridCoords));
                gridCoords.CellPos.Y -= gridCoords.CellPos.Y + SizeInGridTiles.Y - CeilSize.Y;
                deltaMove.Y = 0;
                Logger.LogInfo("Collided down right 2");
            }
        }
    }

    private void SanityCheck()
    {
        // -----------------------------------------------
        var (finalCell, finalCellPos) = Entity.GetGridCoords(Parent);
        var hasCollision = Parent.Collider.HasCollision(finalCell.X, finalCell.Y);
        if (hasCollision && PreviousMoveCollisions.Count == 0)
            Logger.LogInfo("Moved into collision tile!");
        // -----------------------------------------------
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

                if (deltaMove.X != 0)
                {
                    HandleHorizontalMovement(preUpdateCoords, ref gridCoords, ref deltaMove);
                }

                gridCoords.Update();
                preUpdateCoords = gridCoords;
                gridCoords.CellPos.Y += deltaMove.Y / steps;

                if (deltaMove.Y != 0)
                {
                    HandleVerticalMovement(preUpdateCoords, ref gridCoords, ref deltaMove);
                }

                gridCoords.Update();

                n++;
            }

            var finalPosition = (gridCoords.Cell + gridCoords.CellPos) * World.DefaultGridSize;
            Parent.Position.Set(finalPosition);
            SanityCheck();
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
