namespace MyGame;

public class SlugBehaviour : EnemyBehaviour
{
    private Enemy? _parent;
    public Enemy Parent => _parent ?? throw new InvalidOperationException();

    private float _speed = 50f;
    private float _gravity = 800f;
    private float _turnDistanceFromEdge = 0.1f;

    public override void Initialize(Enemy parent)
    {
        _parent = parent;
    }

    public override void Update(float deltaSeconds)
    {
        if (!Parent.CanMove)
            return;

        var collisions = Parent.World.HandleCollisions(Parent, Parent.Velocity, deltaSeconds);

        if ((collisions & CollisionDir.Left) != 0)
        {
            Parent.Velocity.Delta = new Vector2(_speed, 0);
        }
        else if ((collisions & CollisionDir.Right) != 0)
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

        if (!Parent.World.IsGrounded(Parent, Parent.Velocity))
        {
            Parent.Velocity.Y += _gravity * deltaSeconds;
        }

        if (Math.Abs(Parent.Velocity.X) < _speed * 0.5f)
        {
            Parent.Velocity.X += Parent.Velocity.X;
        }

        if (!Parent.World.IsGrounded(Parent, Parent.Velocity))
            return;


        var shouldTurn = Parent.Velocity.X > 0 && !Parent.Collider.HasCollision(Parent.Cell.X + 1, Parent.Cell.Y + 1) &&
                         Parent.CellPos.X > (1.0f - _turnDistanceFromEdge);

        if (!shouldTurn)
        {
            shouldTurn |= Parent.Velocity.X < 0 && !Parent.Collider.HasCollision(Parent.Cell.X - 1, Parent.Cell.Y + 1) &&
                          Parent.CellPos.X < _turnDistanceFromEdge;
        }

        if (shouldTurn)
        {
            Parent.Velocity.X *= -1;
            Parent.FreezeMovement(1.0f);
        }
    }
}
