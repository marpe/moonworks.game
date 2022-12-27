using MyGame.Entities;

namespace MyGame;

public class SlugBehaviour : EnemyBehaviour
{
    private Enemy? _parent;
    public Enemy Parent => _parent ?? throw new InvalidOperationException();

    private float _speed = 50f;
    private Coroutine? _destroyRoutine;

    public override void Initialize(Enemy parent)
    {
        _parent = parent;
    }

    private IEnumerator DestroyOnDeath()
    {
        yield return Coroutine.WaitForSeconds(.5f);
        Parent.IsDestroyed = true;
    }

    public override void Update(float deltaSeconds)
    {
        if (Parent.IsDead)
        {
            _destroyRoutine ??= Parent.CoroutineManager.StartCoroutine(DestroyOnDeath());
            return;
        }

        if (Parent.TotalTimeActive < Parent.FreezeMovementUntil)
            return;

        if (!Parent.Mover.IsGrounded(Parent.Velocity))
        {
            Parent.Velocity.X = 0;
        }
        else
        {
            if (Parent.Velocity.X == 0)
                Parent.Velocity.X = Random.Shared.NextSingle() >= 0.5f ? _speed : -_speed;
            else
                Parent.Velocity.X = Math.Sign(Parent.Velocity.X) * _speed;

            var nextPosition = Parent.Position.Current + Parent.Velocity * deltaSeconds;

            if (Parent.Velocity.X > 0)
            {
                nextPosition.X += Parent.Size.X;
                nextPosition.X = MathF.Ceil(nextPosition.X - 1);
            }

            var nextCellPosition = Entity.ToCell(nextPosition);

            if (!Parent.HasCollision(nextCellPosition.X, nextCellPosition.Y + 1))
            {
                Parent.Velocity.X *= -1;
                Parent.FreezeMovement(1.0f);
                return;
            }
        }

        Parent.Mover.PerformMove(Parent.Velocity, deltaSeconds);

        if (Mover.HasCollisionInDirection(CollisionDir.Left, Parent.Mover.MoveCollisions))
        {
            Parent.Velocity.X = _speed;
        }

        if (Mover.HasCollisionInDirection(CollisionDir.Right, Parent.Mover.MoveCollisions))
        {
            Parent.Velocity.X = -_speed;
        }

        Velocity.ApplyFriction(Parent.Velocity);

        Parent.FacingDirection = Parent.Velocity.X > 0 ? FacingDirection.Right : FacingDirection.Left;
        Parent.Draw.Flip = Parent.FacingDirection == FacingDirection.Right ? SpriteFlip.None : SpriteFlip.FlipHorizontally;

        if (!Parent.Mover.IsGrounded(Parent.Velocity))
        {
            Parent.Velocity.Y += Parent.World.Gravity * deltaSeconds;
        }
    }
}
