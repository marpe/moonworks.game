namespace MyGame.Utils;

public record struct UPoint(uint X, uint Y)
{
    public static readonly UPoint One = new(1u, 1u);

    public static UPoint operator -(UPoint a, UPoint b)
    {
        return new UPoint(a.X - b.X, a.Y - b.Y);
    }
    
    public static UPoint operator +(UPoint a, UPoint b)
    {
        return new UPoint(a.X + b.X, a.Y + b.Y);
    }
    
    public static UPoint operator +(UPoint a, uint b)
    {
        return new UPoint(a.X + b, a.Y + b);
    }
    
    public static UPoint operator -(UPoint a, uint b)
    {
        return new UPoint(a.X - b, a.Y - b);
    }
    
    public static UPoint operator *(UPoint a, uint b)
    {
        return new UPoint(a.X * b, a.Y * b);
    }

    public static UPoint operator /(UPoint p, int n)
    {
        return new UPoint((uint)(p.X / n), (uint)(p.Y / n));
    }
    
    public static UPoint operator /(UPoint p, uint n)
    {
        return new UPoint(p.X / n, p.Y / n);
    }
    
    public static UPoint operator /(UPoint a, UPoint b)
    {
        return new UPoint(a.X / b.X, a.Y / b.Y);
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

    public readonly Vector2 ToVec2() => new(X, Y);
    public readonly Point ToPoint() => new((int)X, (int)Y);
}
