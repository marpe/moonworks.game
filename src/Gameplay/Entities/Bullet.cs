using MoonWorks.Collision.Float;

namespace MyGame.Entities;

public class Bullet : Entity
{
    public float Timer;
    public float Lifetime = 3f;
    public Mover Mover = new();

    public Velocity Velocity = new()
    {
        Delta = Vector2.Zero,
        Friction = new Vector2(1f, 1f),
    };

    public override void Initialize(World world)
    {
        Draw.TexturePath = ContentPaths.animations.bullet_aseprite;
        base.Initialize(world);
        Mover.Initialize(this);
        if (Velocity.Delta.X < 0)
            Draw.Flip = SpriteFlip.FlipHorizontally;
    }

    public override void Update(float deltaSeconds)
    {
        if (IsDestroyed)
            return;

        Timer += deltaSeconds;
        Mover.PerformMove(Velocity, deltaSeconds);
        var radius = MathF.Min(Size.X, Size.Y) * 0.5f;
        var circle = new Circle(radius);
        
        World.Entities.ForEach((entity) =>
        {
            if (entity is not Enemy enemy)
                return;
            if (enemy.IsDead || enemy.IsDestroyed)
                return;
            var other = new MoonWorks.Collision.Float.Rectangle(0, 0, enemy.Size.X, enemy.Size.Y);
            if (NarrowPhase.TestCollision(circle, new Transform2D(Position + Size.ToVec2() * 0.5f), other, new Transform2D(enemy.Position)))
            {
                Destroy();
                enemy.IsDead = true;
                enemy.Draw.Squash = new Vector2(2.0f, 2.0f);
                enemy.Draw.IsAnimating = false;
            }   
        });

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
