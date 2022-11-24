namespace MyGame;

[CustomInspector(typeof(GroupInspector))]
[DebuggerDisplay("{DebugDisplayString,nq}")]
public class Position
{
    public string DebugDisplayString => string.Concat(Current.X.ToString(), " ", Current.Y.ToString());

    public Vector2 Current { get; private set; }
    public Vector2 Previous { get; private set; }
    public Vector2 Initial { get; private set; } = Vector2.Zero;

    /// Position used during render
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

    public void SetX(float x)
    {
        Current = new Vector2(x, Current.Y);
    }

    public void SetY(float y)
    {
        Current = new Vector2(Current.X, y);
    }

    public void DeltaMove(Vector2 deltaMove)
    {
        Current += deltaMove;
    }

    public void DeltaMoveX(float dx) => SetX(Current.X + dx);
    public void DeltaMoveY(float dy) => SetY(Current.Y + dy);

    public void SetPrevAndCurrent(Vector2 position)
    {
        Current = Previous = position;
    }

    public static implicit operator Vector2(Position position)
    {
        return position.Current;
    }
}
