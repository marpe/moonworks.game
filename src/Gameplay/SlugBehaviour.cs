﻿namespace MyGame;

public class SlugBehaviour : EnemyBehaviour
{
    private Enemy? _parent;
    public Enemy Parent => _parent ?? throw new InvalidOperationException();

    private float _speed = 50f;
    private float _turnDistanceFromEdge = 0.1f;

    public override void Initialize(Enemy parent)
    {
        _parent = parent;
    }

    public override void Update(float deltaSeconds)
    {
        if (!Parent.CanMove)
            return;

        var collisions = Parent.Mover.PerformMove(Parent.Velocity, deltaSeconds);

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

        if (!Parent.Mover.IsGrounded(Parent.Velocity))
        {
            Parent.Velocity.Y += Parent.World.Gravity * deltaSeconds;
        }

        if (Math.Abs(Parent.Velocity.X) < _speed * 0.5f)
        {
            Parent.Velocity.X += Parent.Velocity.X;
        }

        if (!Parent.Mover.IsGrounded(Parent.Velocity))
            return;

        var (cell, cellPos) = Entity.GetGridCoords(Parent);

        var shouldTurn = Parent.Velocity.X > 0 && !Parent.Collider.HasCollision(cell.X + 1, cell.Y + 1) && cellPos.X > (1.0f - _turnDistanceFromEdge);

        if (!shouldTurn)
        {
            shouldTurn |= Parent.Velocity.X < 0 && !Parent.Collider.HasCollision(cell.X - 1, cell.Y + 1) && cellPos.X < _turnDistanceFromEdge;
        }

        if (shouldTurn)
        {
            Parent.Velocity.X *= -1;
            Parent.FreezeMovement(1.0f);
        }
    }
}