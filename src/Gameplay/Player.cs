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

    public Matrix4x4 LastTransform = Matrix4x4.Identity;

    public override void Initialize(World world)
    {
        Behaviour.Initialize(this);
        Mover.Initialize(this);
        base.Initialize(world);
    }

    public void Update(float deltaSeconds, PlayerCommand command)
    {
        Behaviour.Update(deltaSeconds, command);
        base.Update(deltaSeconds);
    }

    public Matrix4x4 GetTransform(double alpha)
    {
        var xform = Matrix3x2.CreateTranslation(-Origin.X, -Origin.Y) *
                    Matrix3x2.CreateScale(EnableSquash ? Squash : Vector2.One) *
                    Matrix3x2.CreateTranslation(Position.Lerp(alpha));
        LastTransform = xform.ToMatrix4x4();
        return LastTransform;
    }
}
