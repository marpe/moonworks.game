using MyGame.Cameras;

namespace MyGame.Debug;

public static class CameraDebug
{
    [CVar("cam.debug", "Toggle camera debugging")]
    public static bool DebugCamera;

    public static void DrawCameraBounds(Renderer renderer, Camera camera)
    {
        if (!DebugCamera)
            return;

        var cameraBounds = camera.ZoomedBounds;
        var (boundsMin, boundsMax) = (cameraBounds.Min, cameraBounds.Max);
        renderer.DrawRectOutline(boundsMin, boundsMax, Color.Red, 1f);

        var offset = camera.TargetPosition - camera.Position;
        var dz = camera.DeadZone / 2;
        var lengthX = MathF.Abs(offset.X) - dz.X;
        var lengthY = MathF.Abs(offset.Y) - dz.Y;
        var isDeadZoneActive = lengthX > 0 || lengthY > 0;
        if (isDeadZoneActive)
        {
            var pointOnDeadZone = new Vector2(
                MathF.Clamp(camera.TargetPosition.X, camera.Position.Current.X - dz.X, camera.Position.Current.X + dz.X),
                MathF.Clamp(camera.TargetPosition.Y, camera.Position.Current.Y - dz.Y, camera.Position.Current.Y + dz.Y)
            );
            renderer.DrawLine(pointOnDeadZone, camera.TargetPosition, Color.Red);
        }

        renderer.DrawRectOutline(camera.Position - dz, camera.Position + dz, Color.Magenta * (isDeadZoneActive ? 1.0f : 0.33f));
        renderer.DrawPoint(camera.TargetPosition, Color.Cyan, 4f);

        var posInLevel = camera.Position - camera.LevelBounds.MinVec();
        var cameraMin = posInLevel - camera.ZoomedSize * 0.5f;
        var cameraMax = posInLevel + camera.ZoomedSize * 0.5f;

        if (camera.Velocity.X < 0 && cameraMin.X < camera.BrakeZone.X)
        {
            renderer.DrawLine(camera.Position, camera.Position - new Vector2(camera.BrakeZone.X - cameraMin.X, 0), Color.Red);
        }
        else if (camera.Velocity.X > 0 && cameraMax.X > camera.LevelBounds.Width - camera.BrakeZone.X)
        {
            renderer.DrawLine(camera.Position, camera.Position + new Vector2(cameraMax.X - (camera.LevelBounds.Width - camera.BrakeZone.X), 0), Color.Red);
        }

        if (camera.Velocity.Y < 0 && cameraMin.Y < camera.BrakeZone.Y)
        {
            renderer.DrawLine(camera.Position, camera.Position - new Vector2(0, camera.BrakeZone.Y - cameraMin.Y), Color.Red);
        }
        else if (camera.Velocity.Y > 0 && cameraMax.Y > camera.LevelBounds.Height - camera.BrakeZone.Y)
        {
            renderer.DrawLine(camera.Position, camera.Position + new Vector2(0, cameraMax.Y - (camera.LevelBounds.Height - camera.BrakeZone.Y)), Color.Red);
        }

        renderer.DrawRectOutline(camera.LevelBounds.MinVec() + camera.BrakeZone, camera.LevelBounds.MaxVec() - camera.BrakeZone, Color.Green);
    }
}
