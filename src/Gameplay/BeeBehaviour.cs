using MyGame.Entities;

namespace MyGame;

public class BeeBehaviour : EnemyBehaviour
{
    private Enemy? _parent;
    public Enemy Parent => _parent ?? throw new InvalidOperationException();

    private float _speed = 2f;
    private float _radius = 25f;

    public override void Initialize(Enemy parent)
    {
        _parent = parent;
    }

    public override void Update(float deltaSeconds)
    {
        if (Parent.IsDead)
        {
            Parent.Position.Current += Parent.Velocity * deltaSeconds;

            if (Parent.Position.Current.Y >= Parent.World.Level.Bounds.Bottom)
                Parent.IsDestroyed = true;

            Parent.Velocity.Y += Parent.World.Gravity * deltaSeconds;
            return;
        }

        var t = Parent.TimeOffset + Parent.TotalTimeActive * _speed;
        var deltaMove = new Vector2(
            MathF.Cos(t) * 2.0f,
            MathF.Cos(t) * MathF.Cos(t) - MathF.Sin(t) * MathF.Sin(t)
        ) * 2.0f * _radius;
        Parent.Velocity.Delta = deltaMove;
        Parent.Position.Current += Parent.Velocity * deltaSeconds;

        if (Parent.Velocity.X > 0)
        {
            Parent.Draw.Flip = SpriteFlip.None;
        }
        else if (Parent.Velocity.X < 0)
        {
            Parent.Draw.Flip = SpriteFlip.FlipHorizontally;
        }
    }
}
