﻿namespace MyGame;

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

    public override void Update(float deltaSeconds)
    {
        if (IsDestroyed)
            return;

        Timer += deltaSeconds;
        var collisions = Mover.PerformMove(Velocity, deltaSeconds);
        var distance = 5f;
        
        for (var i = World.Enemies.Count - 1; i >= 0; i--)
        {
            var offset = World.Enemies[i].Bounds.Center - Bounds.Center;
            if (offset.LengthSquared() <= distance * distance)
            {
                IsDestroyed = true;
                World.Enemies[i].IsDestroyed = true;
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

        if (collisions != CollisionDir.None || Timer >= Lifetime)
            IsDestroyed = true;

        base.Update(deltaSeconds);
    }
}