namespace MyGame;

[DebuggerDisplay("{DebugDisplayString,nq}")]
public class Position
{
    public string DebugDisplayString => string.Concat(Current.X.ToString(), " ", Current.Y.ToString());

    public Vector2 Current;

    [HideInInspector] public Vector2 Previous { get; private set; }

    [HideInInspector] public Vector2 Initial { get; private set; } = Vector2.Zero;

    /// Position used during render to interpolate with
    [HideInInspector]
    public Vector2 LastUpdatePosition { get; private set; }

    public Position()
    {
    }

    public Position(Vector2 position)
    {
        Current = Previous = position;
    }

    public void Initialize()
    {
        Initial = LastUpdatePosition = Previous = Current;
    }

    public void SetPrevAndCurrent(Vector2 position)
    {
        Current = Previous = position;
    }

    public static implicit operator Vector2(Position position)
    {
        return position.Current;
    }

    public void SetLastUpdatePosition()
    {
        LastUpdatePosition = Current;
    }
}
