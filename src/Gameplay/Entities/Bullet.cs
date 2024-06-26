﻿using MoonWorks.Collision.Float;
using MoonWorks.Math.Fixed;
using Vector2 = MoonWorks.Math.Float.Vector2;

namespace MyGame.Entities;

public class Bullet : Entity
{
    public float Timer;
    public float Lifetime = 3f;
    public Mover Mover = new();
    private Action<Entity> _collisionCheck;

    public Velocity Velocity = new()
    {
        Delta = Vector2.Zero,
        Friction = new Vector2(1f, 1f),
    };

    public Bullet()
    {
        _collisionCheck = CollisionCheck;
    }

    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.bullet_aseprite;
        base.Initialize(world);
        Mover.Initialize(this);
        if (Velocity.Delta.X < 0)
            Draw.Flip = SpriteFlip.FlipHorizontally;
    }

    private void CollisionCheck(Entity entity)
    {
        if (entity is not Enemy enemy) return;
        if (enemy.IsDead || enemy.IsDestroyed) return;
        var other = new MyGame.MathExtras.Rectangle(0, 0, enemy.Size.X, enemy.Size.Y);
        var radius = MathF.Min(Size.X, Size.Y) * 0.5f;
        var circle = new Circle(radius);

        var circleTransform = new Transform2D(Position + Size.ToVec2() * 0.5f);
        var rectTransform = new Transform2D(enemy.Position);
        // if (NarrowPhase.TestCollision(circle, circleTransform, other, rectTransform))
        if (NarrowPhase.TestCircleRectangleOverlap(circle, circleTransform, other, rectTransform))
        {
            Destroy();
            enemy.IsDead = true;
            enemy.Draw.Squash = new Vector2(2.0f, 2.0f);
            enemy.Draw.IsAnimating = false;
        }
    }

    public override void Update(float deltaSeconds)
    {
        if (IsDestroyed)
            return;

        Timer += deltaSeconds;
        Mover.PerformMove(Velocity, deltaSeconds);

        World.Entities.ForEach(_collisionCheck);

        /*var levelSize = GetLevelSize(World.LdtkRaw, 0);
        var xs = new Point(Math.Max(0, Cell.X - 1),  Math.Min(levelSize.X - 1, Cell.X + 1));
        var ys = new Point(Math.Max(0, Cell.Y - 1),  Math.Min(levelSize.Y - 1, Cell.Y + 1));
        for (var x = xs.X; x < xs.Y; x++)
        {
            for (var y = ys.X; y < ys.Y; y++)
            {
                
            }
        }
        */

        if (Mover.MoveCollisions.Count > 0 || Timer >= Lifetime)
            Destroy();

        base.Update(deltaSeconds);
    }
}
