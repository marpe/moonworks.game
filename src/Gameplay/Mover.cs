namespace MyGame;

public class Mover
{
    private Entity? _parent;
    public Entity Parent => _parent ?? throw new InvalidOperationException();

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
        if (Parent.HasCollision(position, Parent.Size))
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

    private bool TryGetValidPosition(out Vector2 position)
    {
        var startPosition = position = Parent.Position.Current;
        startPosition = startPosition.Floor();
        var levelSize = Parent.World.Level.Size;

        for (var dy = 0; dy < levelSize.Y; dy++)
        {
            var y = (startPosition.Y + dy);
            if (y >= levelSize.Y)
                y = startPosition.Y - y % levelSize.Y;
            for (var dx = 0; dx < levelSize.X; dx++)
            {
                var x = (startPosition.X + dx);
                if (x >= levelSize.X)
                    x = startPosition.X - x % levelSize.X;

                if (Parent.HasCollision(new Vector2(x, y), Parent.Size))
                    continue;

                position = new Vector2(x, y);
                return true;
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
                if (Parent.HasCollision(position, Parent.Size))
                {
                    float intersection;

                    if (deltaMove.X > 0)
                        intersection = MathF.Loop(position.X + Parent.Size.X, World.DefaultGridSize) % World.DefaultGridSize;
                    else
                        intersection = MathF.Loop(position.X, World.DefaultGridSize) - World.DefaultGridSize;

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
                if (Parent.HasCollision(position, Parent.Size))
                {
                    float intersection;
                    if (deltaMove.Y > 0)
                        intersection = MathF.Loop(position.Y + Parent.Size.Y, World.DefaultGridSize) % World.DefaultGridSize;
                    else
                        intersection = MathF.Loop(position.Y, World.DefaultGridSize) - World.DefaultGridSize;

                    var direction = intersection > 0 ? CollisionDir.Down : CollisionDir.Up;
                    MoveCollisions.Add(new CollisionResult(direction, prev, position, new Vector2(0, intersection)));
                    position.Y -= intersection;
                    velocity.Y = deltaMove.Y = 0;
                }
            }
        }

        Parent.Position.Current = position;
        if (Parent.HasCollision(Parent.Position.Current, Parent.Size))
            Logger.LogInfo("Moved into collision tile!");

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
