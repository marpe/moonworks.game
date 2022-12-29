namespace MyGame.Entities;

[CustomInspector<GroupInspector>]
public class Player : Entity
{
    public bool IsJumping;
    public float JumpHoldTime = 0.3f;
    public float JumpSpeed = -300f;
    public float LastJumpStartTime;
    public float LastOnGroundTime;
    public float Speed = 20f;
    private static Vector2 _savedPos;

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
    
    #region Console Commands
    [ConsoleHandler("save_pos")]
    public static void SavePos(Vector2? position = null)
    {
        if (!Shared.Game.World.IsLoaded)
            return;
        _savedPos = position ?? Shared.Game.World.Entities.First<Player>().Position;
        Shared.Console.Print($"Saved position: {_savedPos.ToString()}");
    }

    [ConsoleHandler("load_pos")]
    public static void LoadPos(Vector2? position = null)
    {
        if (!Shared.Game.World.IsLoaded)
            return;

        var loadPos = position ?? _savedPos;
        Shared.Game.World.Entities.First<Player>().Position.SetPrevAndCurrent(loadPos);
        Shared.Console.Print($"Loaded position: {loadPos.ToString()}");
    }

    [ConsoleHandler("unstuck")]
    public static void Unstuck()
    {
        if (!Shared.Game.World.IsLoaded)
            return;
        Shared.Game.World.Entities.First<Player>().Mover.Unstuck();
    }
    #endregion
}
