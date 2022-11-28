namespace MyGame;

public readonly record struct CollisionResult(CollisionDir Direction, Vector2 PreviousPosition, Vector2 Position, Vector2 Intersection)
{
    public readonly CollisionDir Direction = Direction;
    public readonly Vector2 PreviousPosition = PreviousPosition;
    public readonly Vector2 Position = Position;
    public readonly Vector2 Intersection = Intersection;
}
