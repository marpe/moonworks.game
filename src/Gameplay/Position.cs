namespace MyGame;

[DebuggerDisplay("{DebugDisplayString,nq}")]
public class Position
{
    public string DebugDisplayString => string.Concat(Current.X.ToString(), " ", Current.Y.ToString());

    public Vector2 Current;
    
    [HideInInspector]
    public Vector2 Previous { get; private set; }

    [HideInInspector]
    public Vector2 Initial { get; private set; } = Vector2.Zero;

    /// Position used during render
    [HideInInspector]
    public Vector2 ViewPosition { get; private set; }

    /// Lerp between previous and current using alpha and update previous
    public Vector2 Lerp(double alpha)
    {
        ViewPosition = Vector2.Lerp(Previous, Current, (float)alpha);
        Previous = Current;
        return ViewPosition;
    }

    public Position()
    {
    }

    public Position(Vector2 position)
    {
        Current = Previous = position;
    }

    public void Initialize()
    {
        Initial = Previous = Current;
    }

    public void SetPrevAndCurrent(Vector2 position)
    {
        Current = Previous = position;
    }

    public static implicit operator Vector2(Position position)
    {
        return position.Current;
    }
}
