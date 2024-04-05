using MyGame.Entities;

namespace MyGame;

public class Mover
{
    private Entity? _parent;
    public Entity Parent => _parent ?? throw new InvalidOperationException();

    public List<CollisionResult> PreviousMoveCollisions = new();
    public List<CollisionResult> MoveCollisions = new();
    public List<CollisionResult> ContinuedMoveCollisions = new();

    public List<CollisionResult> GroundCollisions = new();

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
        GroundCollisions.Clear();

        if (velocity.Y != 0)
            return false;

        var position = Parent.Position + Vector2.UnitY;
        if (Parent.HasCollision(position, Parent.Size.ToVec2()))
            GroundCollisions.Add(new CollisionResult(CollisionDir.Down, Parent.Position, position, Vector2.UnitY, position));

        return GroundCollisions.Count > 0;
    }

    private bool TryGetValidPosition(out Vector2 position)
    {
        var startPosition = position = Parent.Position.Current;
        startPosition = startPosition.Floor();
        var levelSize = Parent.World.Level.Size;
        var levelPos = Parent.World.Level.WorldPos;

        for (var dy = 0; dy < levelSize.Y; dy++)
        {
            var y = (startPosition.Y + dy);
            if (y >= levelPos.Y + levelSize.Y)
                y = startPosition.Y - y % levelSize.Y;
            for (var dx = 0; dx < levelSize.X; dx++)
            {
                var x = (startPosition.X + dx);
                if (x >= levelPos.X + levelSize.X)
                    x = startPosition.X - x % levelSize.X;

                if (Parent.HasCollision(new Vector2(x, y), Parent.Size.ToVec2()))
                    continue;

                position = new Vector2(x, y);
                return true;
            }
        }

        return false;
    }

#if ENABLE_SANITY_CHECK
    private bool SanityCheck(Vector2 position, string message)
    {
        if (Parent.HasCollision(position, Parent.Size))
        {
            Logs.LogInfo(message);
            return true;
        }

        return false;
    }
#endif

    public void Unstuck()
    {
        if (TryGetValidPosition(out var validPosition))
        {
            var deltaPos = validPosition - Parent.Position.Current;
            Logs.LogInfo($"Moving entity from {Parent.Position.Current} to {validPosition} ({deltaPos.X}, {deltaPos.Y})");
            Parent.Position.SetPrevAndCurrent(validPosition);
            return;
        }

        Logs.LogError("Couldn't find a suitable position");
    }

    public void PerformMove(Velocity velocity, float deltaSeconds)
    {
        PreviousMoveCollisions.Clear();
        PreviousMoveCollisions.AddRange(MoveCollisions);
        MoveCollisions.Clear();
        ContinuedMoveCollisions.Clear();

        if (velocity.Delta.LengthSquared() == 0)
            return;

#if ENABLE_SANITY_CHECK
        if (SanityCheck(Parent.Position.Current, "Already colliding!"))
        {
            velocity.X = velocity.Y = 0;
            return;
        }
#endif

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
                if (Parent.HasCollision(position, Parent.Size.ToVec2()))
                {
                    float intersection;

                    if (deltaMove.X > 0)
                        intersection = MathF.Loop(position.X + Parent.Size.X, World.DefaultGridSize) % World.DefaultGridSize;
                    else
                        intersection = prev.X >= 0 && position.X < 0 ? position.X : MathF.Loop(position.X, World.DefaultGridSize) - World.DefaultGridSize;

                    var direction = intersection > 0 ? CollisionDir.Right : CollisionDir.Left;
                    var resolved = position;
                    resolved.X -= intersection;
                    MoveCollisions.Add(new CollisionResult(direction, prev, position, new Vector2(intersection, 0), resolved));
#if ENABLE_SANITY_CHECK
                    SanityCheck(resolved, "Moving along x-axis resulted in moving into a collision tile!");
#endif
                    position = resolved;
                    velocity.X = deltaMove.X = 0;
                }
            }

            if (deltaMove.Y != 0)
            {
                var dy = deltaMove.Y / steps;
                var prev = position;
                position += new Vector2(0, dy);
                if (Parent.HasCollision(position, Parent.Size.ToVec2()))
                {
                    float intersection;
                    if (deltaMove.Y > 0)
                        intersection = MathF.Loop(position.Y + Parent.Size.Y, World.DefaultGridSize) % World.DefaultGridSize;
                    else
                        intersection = prev.Y >= 0 && position.Y < 0 ? position.Y : MathF.Loop(position.Y, World.DefaultGridSize) - World.DefaultGridSize;

                    var direction = intersection > 0 ? CollisionDir.Down : CollisionDir.Up;
                    var resolved = position;
                    resolved.Y -= intersection;
                    MoveCollisions.Add(new CollisionResult(direction, prev, position, new Vector2(0, intersection), resolved));
#if ENABLE_SANITY_CHECK
                    SanityCheck(resolved, "Moving along y-axis resulted in moving into a collision tile!");
#endif
                    position = resolved;
                    velocity.Y = deltaMove.Y = 0;
                }
            }
        }

        Parent.Position.Current = position;

#if ENABLE_SANITY_CHECK
        SanityCheck(Parent.Position.Current,
            "Post x/y update resulted in moving into a collision tile"); // one last check, because I don't trust anyone, including myself
#endif

        Velocity.ApplyFriction(velocity, deltaSeconds);

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
