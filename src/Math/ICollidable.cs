﻿namespace MyGame;

public interface ICollidable
{
    IEnumerable<IShape2D> Shapes { get; }
    AABB2D AABB { get; }
    AABB2D TransformedAABB(Transform2D transform);
}