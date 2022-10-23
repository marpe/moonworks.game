﻿namespace MyGame.Graphics;

public struct UV
{
    public Vector2 Position { get; }
    public Vector2 Dimensions { get; }

    public Vector2 TopLeft { get; }
    public Vector2 TopRight { get; }
    public Vector2 BottomLeft { get; }
    public Vector2 BottomRight { get; }

    public UV(Vector2 position, Vector2 dimensions)
    {
        Position = position;
        Dimensions = dimensions;

        TopLeft = Position;
        TopRight = Position + new Vector2(Dimensions.X, 0);
        BottomLeft = Position + new Vector2(0, Dimensions.Y);
        BottomRight = Position + new Vector2(Dimensions.X, Dimensions.Y);
    }
}
