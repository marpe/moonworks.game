﻿namespace MyGame.Utils;

public struct UPoint
{
    public uint X;
    public uint Y;

    public UPoint(uint x, uint y)
    {
        X = x;
        Y = y;
    }
    
    public static UPoint operator /(UPoint p, int n)
    {
        return new UPoint((uint)(p.X / n), (uint)(p.Y / n));
    }
    
    public static implicit operator Point(UPoint p)
    {
        return new Point((int)p.X, (int)p.Y);
    }
    
    public static implicit operator UPoint(Point p)
    {
        return new UPoint((uint)p.X, (uint)p.Y);
    }
    
    public static implicit operator Vector2(UPoint p)
    {
        return new Vector2(p.X, p.Y);
    }
}