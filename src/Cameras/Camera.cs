namespace MyGame.Cameras;

public class Camera
{
    private float _zoom = 4.0f;
    public Vector2 BumpOffset;

    public Vector2 DeadZoneInPercentOfViewport = new(0.004f, 0.001f);

    public Vector2 Position;

    public Vector3 Position3D = new(0, 0, -1000);

    public float Rotation = 0;

    public Quaternion Rotation3D = Quaternion.Identity;

    public Vector2 ShakeOffset;
    public Vector2 TargetOffset;
    public Vector2 TargetPosition;

    /// <summary>This is the "true" position, that was used for the view projection calculation Which has shake and bump and crap applied</summary>
    public Vector2 ViewPosition;

    public int Width => MathF.CeilToInt(Size.X / Zoom);
    public int Height => MathF.CeilToInt(Size.Y / Zoom);

    public Point Size = new Point(480, 270);

    public Bounds Bounds => new(Position.X - Width / 2f, Position.Y - Height / 2f, Width, Height);

    public float Zoom
    {
        get => _zoom;
        set => _zoom = MathF.Clamp(value, 0.001f, 10f);
    }

    public Matrix3x2 View
    {
        get
        {
            ViewPosition = Position + ShakeOffset + BumpOffset;
            return Matrix3x2.CreateTranslation(-ViewPosition.X, -ViewPosition.Y) *
                   Matrix3x2.CreateRotation(Rotation) *
                   Matrix3x2.CreateScale(_zoom) *
                   Matrix3x2.CreateTranslation(Size.X * 0.5f, Size.Y * 0.5f);
        }
    }

    public Matrix4x4 View3D
    {
        get
        {
            var position = new Vector3(Position3D.X, Position3D.Y, Position3D.Z);
            return Matrix4x4.CreateLookAt(
                position,
                position + Vector3.Transform(Vector3.Forward, Rotation3D),
                Vector3.Down
            );
        }
    }

    public int HorizontalFovDegrees = 60;

    public Matrix4x4 GetProjection(uint width, uint height, bool use3D)
    {
        if (!use3D)
        {
            return Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, 0.0001f, 10000f);
        }

        var aspectRatio = width / (float)height;
        var hFov = HorizontalFovDegrees * MathF.Deg2Rad;
        var vFov = MathF.Atan(MathF.Tan(hFov / 2.0f) / aspectRatio) * 2.0f;
        return Matrix4x4.CreatePerspectiveFieldOfView(vFov, aspectRatio, 0.0001f, 10000f);
    }
}
