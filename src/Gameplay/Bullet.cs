namespace MyGame;

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

    public SpriteFlip Flip = SpriteFlip.None;

    public override void Initialize(World world)
    {
        base.Initialize(world);
        Mover.Initialize(this);
        if (Velocity.Delta.X < 0)
            Flip = SpriteFlip.FlipHorizontally;
    }

    private static Point GetLevelSize(LdtkJson ldtk, int levelIndex)
    {
        var isMultiWorld = ldtk.Worlds.Length > 0;
        var levels = isMultiWorld ? ldtk.Worlds[0].Levels : ldtk.Levels;
        var level = levels[levelIndex];
        return new Point((int)(level.PxWid / World.DefaultGridSize), (int)(level.PxHei / World.DefaultGridSize));
    }

    public Matrix4x4 GetTransform(double alpha)
    {
        var xform = Matrix3x2.CreateTranslation(Size * Pivot - Pivot * new Vector2(World.DefaultGridSize, World.DefaultGridSize)) *
                    Matrix3x2.CreateTranslation(Position.Lerp(alpha));
        return xform.ToMatrix4x4();
    }

    public override void Update(float deltaSeconds)
    {
        if (IsDestroyed)
            return;

        Timer += deltaSeconds;
        Mover.PerformMove(Velocity, deltaSeconds);
        var distance = 5f;

        for (var i = World.Enemies.Count - 1; i >= 0; i--)
        {
            var offset = World.Enemies[i].Bounds.Center - Bounds.Center;
            if (offset.LengthSquared() <= distance * distance)
            {
                IsDestroyed = true;
                World.Enemies[i].IsDead = true;
                World.FreezeFrame(0.5f);
                return;
            }
        }

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
            IsDestroyed = true;

        base.Update(deltaSeconds);
    }
}
