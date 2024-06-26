﻿using MyGame.Entities;

namespace MyGame.Debug;

public static class MouseDebug
{
    public static Vector2 MousePivot = new Vector2(0f, 0f);
    public static Point MouseSize = new Point(8, 12);

    public static void DrawMousePosition(Renderer renderer)
    {
        var mouseInWorld = World.GetMouseInWorld();
        if (!Shared.Game.Camera.ZoomedBounds.Contains(mouseInWorld))
            return;
        var mouseCell = Entity.ToCell(mouseInWorld);

        var mouseCellRect = new Rectangle(
            mouseCell.X * World.DefaultGridSize,
            mouseCell.Y * World.DefaultGridSize,
            World.DefaultGridSize,
            World.DefaultGridSize
        );
        renderer.DrawRectOutline(mouseCellRect, Color.Red * 0.5f, 1f);

        var mousePosRect = new Bounds(
            mouseInWorld.X,
            mouseInWorld.Y,
            MouseSize.X,
            MouseSize.Y
        );
        renderer.DrawRectOutline(mousePosRect, Color.Blue * 0.5f);

        var mouseRenderRect = new Bounds(
            mouseInWorld.X,
            mouseInWorld.Y,
            MouseSize.X,
            MouseSize.Y
        );
        renderer.DrawRectOutline(mouseRenderRect, Color.Magenta * 0.5f);

        // renderer.DrawPoint(mouseInWorld, Color.Red, 2f);
    }
}
