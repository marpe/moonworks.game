namespace MyGame;

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

    public PlayerBehaviour Behaviour = new();
    public Mover Mover = new();

    public override void Initialize(World world)
    {
        Behaviour.Initialize(this);
        Mover.Initialize(this);
        base.Initialize(world);
    }

    public void Update(float deltaSeconds, InputHandler input)
    {
        Behaviour.Update(deltaSeconds, input);
        base.Update(deltaSeconds);
    }
}
