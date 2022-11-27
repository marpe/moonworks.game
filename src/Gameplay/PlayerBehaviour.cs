namespace MyGame;

public static class PlayerBinds
{
    public static ButtonBind Right = new();
    public static ButtonBind Left = new();
    public static ButtonBind Jump = new();
    public static ButtonBind Fire1 = new();
    public static ButtonBind Respawn = new();
    public static ButtonBind MoveToMouse = new();

    public static PlayerCommand ToPlayerCommand()
    {
        var cmd = new PlayerCommand();

        if (Right.Active)
            cmd.MovementX += 1;

        if (Left.Active)
            cmd.MovementX += -1;

        cmd.IsFiring = Fire1.WasPressed;
        cmd.Respawn = Respawn.WasPressed;
        cmd.IsJumpDown = Jump.Active;
        cmd.IsJumpPressed = Jump.WasPressed;
        cmd.MoveToMouse = MoveToMouse.Active;

        return cmd;
    }
}

public struct PlayerCommand
{
    public float MovementX;
    public bool IsFiring;
    public bool IsJumpPressed;
    public bool IsJumpDown;
    public bool Respawn;
    public bool MoveToMouse;
}

public class PlayerBehaviour
{
    private Player? _player;

    public Player Player => _player ?? throw new InvalidOperationException();

    public void Initialize(Player player)
    {
        _player = player;
    }

    public void Update(float deltaSeconds, PlayerCommand command)
    {
        if (command.Respawn)
        {
            Player.Position.SetPrevAndCurrent(Player.Position.Initial);
        }

        if (command.IsFiring)
        {
            var direction = Player.Flip == SpriteFlip.FlipHorizontally ? -1 : 1;
            Player.World.SpawnBullet(Player.Position.Current, direction);
        }

        if (Player.Position.Current.Y > 300)
        {
            Player.Position.SetPrevAndCurrent(Player.Position.Initial);
        }

        Player.TotalTime += deltaSeconds;

        if (Player.Mover.IsGrounded(Player.Velocity))
        {
            Player.LastOnGroundTime = Player.TotalTime;
        }

        if (command.MovementX != 0)
        {
            Player.Velocity.X += command.MovementX * Player.Speed;
        }

        if (command.MoveToMouse)
        {
            var mousePosition = Shared.Game.InputHandler.MousePosition;
            var view = Shared.Game.GameScreen.Camera.GetView();
            Matrix3x2.Invert(view, out var invertedView);
            var mouseInWorld = Vector2.Transform(mousePosition, invertedView);

            var offset = mouseInWorld - Player.Position;
            Player.Velocity.Delta = offset * deltaSeconds * 1000f;
        }

        Player.FrameIndex = MathF.IsNearZero(Player.Velocity.X, 0.01f) ? 0 : (uint)(Player.TotalTime * 10) % 2;

        if (!Player.IsJumping && command.IsJumpPressed)
        {
            var timeSinceOnGround = Player.TotalTime - Player.LastOnGroundTime;
            if (timeSinceOnGround < 0.1f)
            {
                Player.Squash = new Vector2(0.6f, 1.4f);
                Player.LastOnGroundTime = 0;
                Player.Velocity.Y = Player.JumpSpeed;
                Player.LastJumpStartTime = Player.TotalTime;
                Player.IsJumping = true;
            }
        }

        if (Player.IsJumping)
        {
            if (!command.IsJumpDown)
            {
                Player.IsJumping = false;
            }
            else
            {
                var timeAirborne = Player.TotalTime - Player.LastJumpStartTime;
                if (timeAirborne > Player.JumpHoldTime)
                {
                    Player.IsJumping = false;
                }
            }
        }

        Player.Mover.PerformMove(Player.Velocity, deltaSeconds);

        if (Mover.HasCollisionInDirection(CollisionDir.Down, Player.Mover.MoveCollisions))
        {
            Player.Squash = new Vector2(1.5f, 0.5f);
        }

        if (Mover.HasCollisionInDirection(CollisionDir.Up, Player.Mover.MoveCollisions))
        {
            Player.IsJumping = false;
        }

        if (Player.Velocity.X > 0)
        {
            Player.Flip = SpriteFlip.None;
        }
        else if (Player.Velocity.X < 0)
        {
            Player.Flip = SpriteFlip.FlipHorizontally;
        }

        if (!Player.Mover.IsGrounded(Player.Velocity) && !Player.IsJumping && !command.MoveToMouse)
        {
            Player.Velocity.Y += Player.World.Gravity * deltaSeconds;
        }

        Player.Squash = Vector2.SmoothStep(Player.Squash, Vector2.One, deltaSeconds * 20f);
    }
}
