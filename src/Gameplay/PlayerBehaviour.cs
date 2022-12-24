namespace MyGame;

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
            var direction = Player.Draw.Flip == SpriteFlip.FlipHorizontally ? -1 : 1;
            Player.World.SpawnBullet(Player.Position.Current, direction);
        }

        /*if (Player.Position.Current.Y > 300)
        {
            Player.Position.SetPrevAndCurrent(Player.Position.Initial);
        }*/

        if (Player.Mover.IsGrounded(Player.Velocity))
        {
            Player.LastOnGroundTime = Player.TotalTimeActive;
        }

        if (command.MovementX != 0)
        {
            Player.Velocity.X += command.MovementX * Player.Speed;
        }

        if (command.MoveToMouse)
        {
            var mousePosition = Shared.Game.InputHandler.MousePosition;
            var view = Shared.Game.Camera.GetView();
            Matrix3x2.Invert(view, out var invertedView);
            var mouseInWorld = Vector2.Transform(mousePosition, invertedView);

            var offset = mouseInWorld - Player.Position;
            Player.Velocity.Delta = offset * deltaSeconds * 1000f;
        }

        Player.Draw.IsAnimating = !MathF.IsNearZero(Player.Velocity.X, 0.01f);
        if (!Player.Draw.IsAnimating)
            Player.Draw.FrameIndex = 0;

        if (!Player.IsJumping && command.IsJumpPressed)
        {
            var timeSinceOnGround = Player.TotalTimeActive - Player.LastOnGroundTime;
            if (timeSinceOnGround < 0.1f)
            {
                Player.Draw.Squash = new Vector2(0.6f, 1.4f);
                Player.LastOnGroundTime = 0;
                Player.Velocity.Y = Player.JumpSpeed;
                Player.LastJumpStartTime = Player.TotalTimeActive;
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
                var timeAirborne = Player.TotalTimeActive - Player.LastJumpStartTime;
                if (timeAirborne > Player.JumpHoldTime)
                {
                    Player.IsJumping = false;
                }
            }
        }

        Player.Mover.PerformMove(Player.Velocity, deltaSeconds);

        if (Mover.HasCollisionInDirection(CollisionDir.Down, Player.Mover.MoveCollisions))
        {
            Player.Draw.Squash = new Vector2(1.5f, 0.5f);
        }

        if (Mover.HasCollisionInDirection(CollisionDir.Up, Player.Mover.MoveCollisions))
        {
            Player.IsJumping = false;
            Player.Draw.Squash = new Vector2(1.5f, 0.5f);
        }

        if (Player.Velocity.X > 0)
        {
            Player.Draw.Flip = SpriteFlip.None;
        }
        else if (Player.Velocity.X < 0)
        {
            Player.Draw.Flip = SpriteFlip.FlipHorizontally;
        }

        if (!Player.Mover.IsGrounded(Player.Velocity) && !Player.IsJumping && !command.MoveToMouse)
        {
            Player.Velocity.Y += Player.World.Gravity * deltaSeconds;
        }

        Player.Draw.Squash = Vector2.SmoothStep(Player.Draw.Squash, Vector2.One, deltaSeconds * 20f);
    }
}
