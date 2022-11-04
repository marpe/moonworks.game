using MyGame.Graphics;

namespace MyGame;

public partial class Entity
{
    public Vector2 InitialPosition;
    public Vector2 PreviousPosition;
    public Vector2 Origin => Pivot * Size;
    public Bounds Bounds => new Bounds(Position.X - Origin.X, Position.Y - Origin.Y, Size.X, Size.Y);
    public Vector2 Center => new Vector2(Position.X + (0.5f - Pivot.X) * Size.X, Position.Y + (0.5f - Pivot.Y) * Size.Y);
}

public partial class Enemy : Entity
{
    public Velocity Velocity = new();
    public SpriteFlip Flip;
    public float TotalTime;
}

public partial class Gun_Pickup : Entity
{
}

public partial class RefTest : Entity
{
}

public partial class Player : Entity
{
    public Velocity Velocity = new()
    {
        Delta = Vector2.Zero,
        Friction = new Vector2(0.84f, 0.98f)
    };

    public uint FrameIndex;
    public float TotalTime;
    public float Speed = 20f;
    public float JumpSpeed = -300f;
    public float LastOnGroundTime;
    public Vector2 Squash = Vector2.One;
    public bool IsJumping;
    public float JumpHoldTime = 0.3f;
    public float LastJumpStartTime;
    public bool EnableSquash = true;
    public SpriteFlip Flip = SpriteFlip.None;

    public void SetPositions(Vector2 position)
    {
        PreviousPosition = Position = position;
    }
}
