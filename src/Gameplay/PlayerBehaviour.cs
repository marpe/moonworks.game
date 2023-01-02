using MyGame.Entities;

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
    private PlayerCommand _command;
    private Coroutine? _locomoteRoutine;
    private float _jumpHeldTimer = 0;
    private IEnumerator? _groundMove;
    private IEnumerator? _airMove;
    private bool _firstGroundUpdate;
    private float _fallDuration;
    private bool _hasJumped;

    public Player Player => _player ?? throw new InvalidOperationException();

    private float _dt;

    public void Initialize(Player player)
    {
        _player = player;

        player.CoroutineManager.StartCoroutine(CheckJumpHeld());
        _groundMove = GroundMove();
        _airMove = AirMove();
        _locomoteRoutine = player.CoroutineManager.StartCoroutine(_groundMove);
        player.CoroutineManager.StartCoroutine(RespawnRoutine());
        player.CoroutineManager.StartCoroutine(WeaponRoutine());
        player.CoroutineManager.StartCoroutine(MouseMoveRoutine());
    }

    private IEnumerator CheckJumpHeld()
    {
        while (true)
        {
            if (_command.IsJumpDown)
                _jumpHeldTimer += _dt;
            else
                _jumpHeldTimer = 0;

            yield return null;
        }
    }

    private IEnumerator RespawnRoutine()
    {
        while (true)
        {
            if (_command.Respawn)
            {
                Player.Position.SetPrevAndCurrent(Player.Position.Initial);
            }

            yield return null;
        }
    }

    private IEnumerator WeaponRoutine()
    {
        while (true)
        {
            if (_command.IsFiring)
            {
                var direction = Player.Draw.Flip == SpriteFlip.FlipHorizontally ? -1 : 1;
                Player.World.SpawnBullet(Player.Position.Current, direction);
                Player.World.SpawnMuzzleFlash(Player.Position.Current, direction);

                Player.Draw.PlayAnimation("Fire");
            }

            yield return null;
        }
    }

    private IEnumerator MouseMoveRoutine()
    {
        while (true)
        {
            if (_command.MoveToMouse)
            {
                var mouseInWorld = World.GetMouseInWorld();

                var offset = mouseInWorld - Player.Center;
                Player.Velocity.Delta = offset * _dt * 1000f;
                Player.Mover.PerformMove(Player.Velocity, _dt);
            }

            yield return null;
        }
    }

    private void ChangeState(IEnumerator nextState, string name)
    {
        _firstGroundUpdate = true;
        
        _fallDuration = 0;
        _hasJumped = false;

        _locomoteRoutine!.Replace(nextState, name);
    }

    private IEnumerator GroundMove()
    {
        while (true)
        {
            var isGrounded = Player.Mover.IsGrounded(Player.Velocity);
            if (!isGrounded)
            {
                ChangeState(_airMove!, nameof(AirMove));
                yield return null;
                continue;
            }

            if (_command.IsJumpPressed || (_firstGroundUpdate && _command.IsJumpDown && _jumpHeldTimer < 0.1f))
            {
                ChangeState(_airMove!, nameof(AirMove));
                yield return null;
                continue;
            }

            _firstGroundUpdate = false;

            // horizontal movement
            if (_command.MovementX != 0)
            {
                Player.Velocity.X += _command.MovementX * Player.Speed * _dt;

                Player.Draw.PlayAnimation("Run");
                Player.Draw.IsAnimating = true;

                UpdateSpriteFlip();
            }

            if (MathF.IsNearZero(Player.Velocity.X, 1f))
            {
                Player.Draw.IsAnimating = false;
                Player.Draw.FrameIndex = 0;
            }

            Player.Mover.PerformMove(Player.Velocity, _dt);

            yield return null;
        }
    }

    private void UpdateSpriteFlip()
    {
        Player.Draw.Flip = Player.Velocity.X switch
        {
            > 0 => SpriteFlip.None,
            < 0 => SpriteFlip.FlipHorizontally,
            _ => Player.Draw.Flip
        };
    }

    private IEnumerator AirMove()
    {
        while (true)
        {
            Player.Draw.PlayAnimation("Run");
            Player.Draw.IsAnimating = false;
            
            if ((_command.IsJumpPressed || _command.IsJumpDown && _jumpHeldTimer < 0.1f) && _fallDuration < 0.1f && !_hasJumped)
            {
                _hasJumped = true;
                Player.Draw.Squash = new Vector2(0.2f, 1.4f);

                // vertical movement
                Player.Velocity.Y = Player.JumpSpeed;

                var jumpTime = 0f;
                while (true)
                {
                    jumpTime += _dt;

                    if (!_command.IsJumpDown)
                        break;

                    if (jumpTime > Player.JumpMaxHoldTime)
                         break;

                    // horizontal movement
                    if (_command.MovementX != 0)
                    {
                        Player.Velocity.X += _command.MovementX * Player.Speed * _dt;
                        UpdateSpriteFlip();
                    }

                    Player.Mover.PerformMove(Player.Velocity, _dt);

                    if (Mover.HasCollisionInDirection(CollisionDir.Up, Player.Mover.MoveCollisions))
                    {
                        Player.Draw.Squash = new Vector2(1.5f, 0.5f);
                        break;
                    }

                    yield return null;
                }
            }

            _fallDuration += _dt;

            // vertical movement
            Player.Velocity.Y += Player.World.Gravity * _dt;

            // horizontal movement
            if (_command.MovementX != 0)
            {
                Player.Velocity.X += _command.MovementX * Player.Speed * _dt;
                UpdateSpriteFlip();
            }

            Player.Mover.PerformMove(Player.Velocity, _dt);

            // land on ground
            if (Mover.HasCollisionInDirection(CollisionDir.Down, Player.Mover.MoveCollisions))
            {
                Player.Draw.Squash = new Vector2(1.5f, 0.5f);
                ChangeState(_groundMove!, nameof(GroundMove));
                yield return null;
                continue;
            }

            yield return null;
        }
    }

    public void Update(float deltaSeconds, PlayerCommand command)
    {
        _dt = deltaSeconds;
        _command = command;
    }
}
