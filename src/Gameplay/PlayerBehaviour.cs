namespace MyGame;

public class PlayerBehaviour
{
    private Player? _player;
    public Player Player => _player ?? throw new InvalidOperationException();

    public void Initialize(Player player)
    {
        _player = player;
    }

    private void HandleInput(InputHandler input, out int movementX, out bool isFiring)
    {
        movementX = 0;
        isFiring = false;
        
        if (input.IsKeyPressed(KeyCode.Insert))
        {
            Player.SetPositions(new Vector2(100, 50));
        }

        if (input.IsKeyDown(KeyCode.Right) ||
            input.IsKeyDown(KeyCode.D))
        {
            movementX += 1;
        }

        if (input.IsKeyDown(KeyCode.Left) ||
            input.IsKeyDown(KeyCode.A))
        {
            movementX += -1;
        }

        if (input.IsKeyPressed(KeyCode.LeftControl))
        {
            isFiring = true;
        }
    }
    
    public void Update(float deltaSeconds, InputHandler input)
    {
        HandleInput(input, out var movementX, out var isFiring);
        var isJumpDown = input.IsKeyDown(KeyCode.Space);
        var isJumpPressed = input.IsKeyPressed(KeyCode.Space);

        if (isFiring)
        {
            var direction = Player.Flip == SpriteFlip.FlipHorizontally ? -1 : 1;
            Player.World.SpawnBullet(Player.Position, direction);
        }
        
        if (Player.Position.Y > 300)
        {
            Player.SetPositions(Player.InitialPosition);
        }

        Player.TotalTime += deltaSeconds;
        Player.FrameIndex = MathF.IsNearZero(Player.Velocity.X) ? 0 : (uint)(Player.TotalTime * 10) % 2;

        if (Player.World.IsGrounded(Player, Player.Velocity))
        {
            Player.LastOnGroundTime = Player.TotalTime;
        }

        if (movementX != 0)
        {
            Player.Velocity.X += movementX * Player.Speed;
        }

        if (!Player.IsJumping && isJumpPressed)
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
            if (!isJumpDown)
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

        var collisions = Player.World.HandleCollisions(Player, Player.Velocity, deltaSeconds);
       
        if ((collisions & CollisionDir.Down) == CollisionDir.Down)
        {
            Player.Squash = new Vector2(1.5f, 0.5f);
        }

        if ((collisions & CollisionDir.Top) == CollisionDir.Top)
        {
            Player.IsJumping = false;
        }

        Velocity.ApplyFriction(Player.Velocity);
        
        if (Player.Velocity.X > 0)
        {
            Player.Flip = SpriteFlip.None;
        }
        else if (Player.Velocity.X < 0)
        {
            Player.Flip = SpriteFlip.FlipHorizontally;
        }

        if (!Player.World.IsGrounded(Player, Player.Velocity) && !Player.IsJumping)
        {
            Player.Velocity.Y += Player.World.Gravity * deltaSeconds;
        }

        Player.Squash = Vector2.SmoothStep(Player.Squash, Vector2.One, deltaSeconds * 20f);
    }
}
