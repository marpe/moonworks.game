using MyGame.Graphics;
using MyGame.TWImGui;
using MyGame.TWImGui.Inspectors;

namespace MyGame;

[CustomInspector(typeof(GroupInspector))]
public partial class Entity
{
    public Vector2 InitialPosition;
    public Vector2 PreviousPosition;
    public Vector2 Origin => Pivot * Size;
    public Bounds Bounds => new(Position.X - Origin.X, Position.Y - Origin.Y, Size.X, Size.Y);
    public Vector2 Center => new(Position.X + (0.5f - Pivot.X) * Size.X, Position.Y + (0.5f - Pivot.Y) * Size.Y);
}

public partial class Enemy : Entity
{
    public SpriteFlip Flip;
    public float TotalTime;
    public Velocity Velocity = new();
    public float TimeOffset;
}

public partial class Gun_Pickup : Entity
{
}

public partial class RefTest : Entity
{
}

public partial class Player : Entity
{
    public bool EnableSquash = true;
    public SpriteFlip Flip = SpriteFlip.None;

    public uint FrameIndex;
    public bool IsJumping;
    public float JumpHoldTime = 0.3f;
    public float JumpSpeed = -300f;
    public float LastJumpStartTime;
    public float LastOnGroundTime;
    public float Speed = 20f;
    public Vector2 Squash = Vector2.One;
    public float TotalTime;

    public Velocity Velocity = new()
    {
        Delta = Vector2.Zero,
        Friction = new Vector2(0.84f, 0.98f),
    };

    public void SetPositions(Vector2 position)
    {
        PreviousPosition = Position = position;
    }
}
