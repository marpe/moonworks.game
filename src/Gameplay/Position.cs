namespace MyGame;

[DebuggerDisplay("{DebugDisplayString,nq}")]
public class Position
{
    [HideInInspector]
    public string DebugDisplayString => string.Concat(Current.X.ToString(), " ", Current.Y.ToString());

    public Vector2 Current;

    [HideInInspector] public Vector2 Initial { get; private set; } = Vector2.Zero;

    /// Position used during render to interpolate with
    [HideInInspector]
    public Vector2 LastUpdatePosition { get; private set; }

    public Position()
    {
    }

    public void Initialize()
    {
        Initial = LastUpdatePosition = Current;
    }

    public void SetPrevAndCurrent(Vector2 position)
    {
        Current = position;
    }

    public static implicit operator Vector2(Position position)
    {
        return position.Current;
    }

    public void SetLastUpdatePosition()
    {
        LastUpdatePosition = Current;
    }

    public Vector2 Lerp(double alpha)
    {
        return Vector2.Lerp(LastUpdatePosition, Current, (float)alpha);
    }
}
