namespace MyGame.Entities;

[CustomInspector<GroupInspector>]
public class Player : Entity
{
    [HideInInspector]
    public bool IsJumping;
    public float JumpHoldTime = 0.3f;
    public float JumpSpeed = -300f;
    public float LastJumpStartTime;
    public float LastOnGroundTime;
    public float Speed = 20f;

    public Velocity Velocity = new()
    {
        Delta = Vector2.Zero,
        Friction = new Vector2(0.84f, 0.98f),
    };

    public PlayerBehaviour Behaviour = new();
    public Mover Mover = new();

    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.player_aseprite;
        Behaviour.Initialize(this);
        Mover.Initialize(this);
        base.Initialize(world);
    }

    public override void Update(float deltaSeconds)
    {
        var command = Binds.Player.ToPlayerCommand();
        Behaviour.Update(deltaSeconds, command);
        base.Update(deltaSeconds);
    }
}