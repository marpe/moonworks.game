namespace MyGame;

public readonly record struct CollisionResult(CollisionDir Direction, Vector2 PreviousPosition, Vector2 Position, Vector2 Intersection,
    Vector2 ResolvedPosition);
