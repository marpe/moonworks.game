namespace MyGame;

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

    public List<CollisionResult> PreviousMoveCollisions = new();
    public List<CollisionResult> MoveCollisions = new();
    public List<CollisionResult> ContinuedMoveCollisions = new();

    public List<CollisionResult> PreviousGroundCollisions = new();
    public List<CollisionResult> GroundCollisions = new();
    public List<CollisionResult> ContinuedGroundCollisions = new();

    private static readonly CollisionResult NoCollision = new();

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

        var position = Parent.Position + Vector2.UnitY;
        if (HasCollision(position))
            GroundCollisions.Add(new CollisionResult(CollisionDir.Down, Parent.Position, position, Vector2.UnitY));

        for (var i = 0; i < PreviousGroundCollisions.Count; i++)
        {
            var prev = PreviousGroundCollisions[i];
            for (var j = 0; j < GroundCollisions.Count; j++)
            {
                var curr = GroundCollisions[j];
                if (prev == curr)
                {
                    ContinuedGroundCollisions.Add(prev);
                    break;
                }
            }
        }

        return GroundCollisions.Count > 0;
    }

    private bool HasCollision(Vector2 position)
    {
        var minCell = GridCoords.ToCell(position);
        var max = new Vector2(
            MathF.Ceil(position.X + Parent.Size.X - 1),
            MathF.Ceil(position.Y + Parent.Size.Y - 1)
        );
        var maxCell = GridCoords.ToCell(max);

        for (var x = minCell.X; x <= maxCell.X; x++)
        {
            for (var y = minCell.Y; y <= maxCell.Y; y++)
            {
                if (Parent.Collider.HasCollision(x, y))
                    return true;
            }
        }

        return false;
    }

    private void SanityCheck()
    {
        // -----------------------------------------------
        var gridCoords = Parent.GridCoords;
        var hasCollision = Parent.Collider.HasCollision(gridCoords.Cell.X, gridCoords.Cell.Y);
        if (hasCollision && PreviousMoveCollisions.Count == 0)
            Logger.LogInfo("Moved into collision tile!");
        // -----------------------------------------------
    }

    public bool TryGetValidPosition(out Vector2 position)
    {
        var startPosition = position = Parent.Position.Current;
        var levelSize = Parent.World.Level.Size;
        int dx, dy;

        for (dy = 0; dy < levelSize.Y; dy++)
        {
            var y = (startPosition.Y + dy);
            if (y >= levelSize.Y)
                y = startPosition.Y - y % levelSize.Y;
            for (dx = 0; dx < levelSize.X; dx++)
            {
                var x = (startPosition.X + dx);
                if (x >= levelSize.X)
                    x = startPosition.X - x % levelSize.X;
                if (!HasCollision(new Vector2(x, y)))
                {
                    position = new Vector2(x, y);
                    return true;
                }
            }
        }

        return false;
    }

    public void Unstuck()
    {
        if (TryGetValidPosition(out var validPosition))
        {
            Parent.Position.SetPrevAndCurrent(validPosition);
            return;
        }

        Logger.LogError("Couldn't find a suitable position");
    }

    public void PerformMove(Velocity velocity, float deltaSeconds)
    {
        PreviousMoveCollisions.Clear();
        PreviousMoveCollisions.AddRange(MoveCollisions);
        MoveCollisions.Clear();
        ContinuedMoveCollisions.Clear();

        if (velocity.Delta.LengthSquared() == 0)
            return;

        var deltaMove = velocity * deltaSeconds;
        var steps = MathF.Ceil((MathF.Abs(deltaMove.X) + MathF.Abs(deltaMove.Y)) / World.DefaultGridSize / 0.33f);
        var position = Parent.Position.Current;

        for (var i = 0; i < steps; i++)
        {
            if (deltaMove.X != 0)
            {
                var dx = deltaMove.X / steps;
                var prev = position;
                position += new Vector2(dx, 0);
                if (HasCollision(position))
                {
                    float intersection;

                    if (deltaMove.X > 0)
                        intersection = (position.X + Parent.Size.X) % World.DefaultGridSize;
                    else
                        intersection = (position.X % World.DefaultGridSize) - World.DefaultGridSize;

                    var direction = intersection > 0 ? CollisionDir.Right : CollisionDir.Left;
                    MoveCollisions.Add(new CollisionResult(direction, prev, position, new Vector2(intersection, 0)));
                    position.X -= intersection;
                    velocity.X = deltaMove.X = 0;
                }
            }

            if (deltaMove.Y != 0)
            {
                var dy = deltaMove.Y / steps;
                var prev = position;
                position += new Vector2(0, dy);
                if (HasCollision(position))
                {
                    float intersection;
                    if (deltaMove.Y > 0)
                        intersection = (position.Y + Parent.Size.Y) % World.DefaultGridSize;
                    else
                        intersection = (position.Y % World.DefaultGridSize) - World.DefaultGridSize;

                    var direction = intersection > 0 ? CollisionDir.Down : CollisionDir.Up;
                    MoveCollisions.Add(new CollisionResult(direction, prev, position, new Vector2(0, intersection)));
                    position.Y -= intersection;
                    velocity.Y = deltaMove.Y = 0;
                }
            }
        }

        Parent.Position.Current = position;
        SanityCheck();

        Velocity.ApplyFriction(velocity);

        for (var i = 0; i < PreviousMoveCollisions.Count; i++)
        {
            var prev = PreviousMoveCollisions[i];
            for (var j = 0; j < MoveCollisions.Count; j++)
            {
                var curr = MoveCollisions[j];
                if (prev == curr)
                {
                    ContinuedMoveCollisions.Add(prev);
                    break;
                }
            }
        }
    }
}
