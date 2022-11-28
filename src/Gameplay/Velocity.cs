namespace MyGame;

[CustomInspector(typeof(GroupInspector))]
[DebuggerDisplay("{DebugDisplayString,nq}")]
public class Velocity
{
    public const float KillThreshold = 0.0005f;
    public Vector2 Delta = Vector2.Zero;
    public Vector2 Friction = new(0.84f, 0.94f);

    [HideInInspector]
    public string DebugDisplayString => string.Concat(X.ToString(), " ", Y.ToString());

    [HideInInspector]
    public float X
    {
        get => Delta.X;
        set => Delta.X = value;
    }

    [HideInInspector]
    public float Y
    {
        get => Delta.Y;
        set => Delta.Y = value;
    }

    public static void ApplyFriction(Velocity velocity)
    {
        velocity.Delta *= velocity.Friction;
        if (MathF.IsNearZero(velocity.X, KillThreshold))
        {
            velocity.X = 0;
        }

        if (MathF.IsNearZero(velocity.Y, KillThreshold))
        {
            velocity.Y = 0;
        }
    }

    public static Vector2 operator *(Velocity velocity, float value)
    {
        return velocity.Delta * value;
    }

    public static implicit operator Vector2(Velocity velocity)
    {
        return velocity.Delta;
    }
}
