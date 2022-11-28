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
            var curPos = Parent.GridCoords;
            var nextCellPosX = curPos.CellPos.X + Parent.Velocity.X * deltaSeconds / World.DefaultGridSize; 

            var dropRight = Parent.Velocity.X > 0 &&
                            !Parent.Collider.HasCollision(curPos.Cell.X + 1, curPos.Cell.Y + 1) &&
                            nextCellPosX + (Parent.Size.X / (float)World.DefaultGridSize) > 1f;
            var dropLeft = Parent.Velocity.X < 0 &&
                           !Parent.Collider.HasCollision(curPos.Cell.X - 1, curPos.Cell.Y + 1) &&
                           nextCellPosX < 0;
        
            if (dropRight || dropLeft)
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
