namespace MyGame;

public class SlugBehaviour : EnemyBehaviour
{
    private Enemy? _parent;
    public Enemy Parent => _parent ?? throw new InvalidOperationException();

    private float _speed = 50f;

    public override void Initialize(Enemy parent)
    {
        _parent = parent;
    }

    public override void Update(float deltaSeconds)
    {
        if (!Parent.CanMove)
            return;

        if (Parent.Mover.IsGrounded(Parent.Velocity))
        {
            var nextPosition = Parent.Position.Current + Parent.Velocity * deltaSeconds;

            if (Parent.Velocity.X > 0)
                nextPosition.X += Parent.Size.X - 1;
            else
                nextPosition.X -= Parent.Size.X;

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
            Parent.Velocity.Delta = new Vector2(_speed, 0);
        }

        if (Mover.HasCollisionInDirection(CollisionDir.Right, Parent.Mover.MoveCollisions))
        {
            Parent.Velocity.Delta = new Vector2(-_speed, 0);
        }

        Velocity.ApplyFriction(Parent.Velocity);

        if (Parent.Velocity.X > 0)
        {
            Parent.Flip = SpriteFlip.None;
        }
        else if (Parent.Velocity.X < 0)
        {
            Parent.Flip = SpriteFlip.FlipHorizontally;
        }

        if (!Parent.Mover.IsGrounded(Parent.Velocity))
        {
            Parent.Velocity.Y += Parent.World.Gravity * deltaSeconds;
        }

        if (Math.Abs(Parent.Velocity.X) < _speed * 0.5f)
        {
            Parent.Velocity.X += Parent.Velocity.X;
        }
    }
}
