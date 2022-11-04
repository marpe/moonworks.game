namespace MyGame.Cameras;

public class Camera
{
    public Bounds PreviousBounds;

    public static Vector2 Viewport => Shared.Game.MainWindow.Size;
    public static Vector2 Scale = Vector2.One;
    public int Width => MathF.CeilToInt(Viewport.X / Scale.X / Zoom);
    public int Height => MathF.CeilToInt(Viewport.Y / Scale.Y / Zoom);

    public Point Size => new(Width, Height);

    public Bounds Bounds => new Bounds(Position.X - Width / 2f, Position.Y - Height / 2f, Width, Height);
    
    private float _zoom = 1.0f;
    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = MathF.Clamp(value, 0.001f, 10f);
        }
    }

    public Vector2 ShakeOffset;
    public Vector2 BumpOffset;
    public Vector2 TargetPosition;
    public Vector2 TargetOffset;
    
    public Vector2 DeadZoneInPercentOfViewport = new Vector2(0.04f, 0.1f);

    public Vector2 Position;
    
    /// <summary>
    /// This is the "true" position, that was used for the view projection calculation
    /// Which has shake and bump and crap applied
    /// </summary>
    public Vector2 ViewPosition;

    public Vector3 Position3D = new(0, 0, -1000);

    
    public Matrix4x4 View
    {
        get
        {
            ViewPosition = Position + ShakeOffset + BumpOffset;
            var view = Matrix4x4.CreateLookAt(
                new Vector3(ViewPosition.X, ViewPosition.Y, 1000),
                new Vector3(ViewPosition.X, ViewPosition.Y, 0),
                Vector3.Up
            );
            return view * Matrix4x4.CreateScale(Zoom, Zoom, 1.0f) *
                   Matrix4x4.CreateTranslation(Viewport.X * 0.5f, Viewport.Y * 0.5f, 0);
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
    
    public Quaternion Rotation3D = Quaternion.Identity;
    public Matrix4x4 Projection
    {
        get
        {
            var viewport = Viewport;
            return Matrix4x4.CreateOrthographicOffCenter(
                0,
                viewport.X,
                viewport.Y,
                0,
                0.0001f,
                4000f
            );
        }
    }

    public Matrix4x4 Projection3D
    {
        get
        {
            var viewport = Viewport;
            var targetHeight = viewport.Y; // / zoom
            var fov = 60 * MathF.Deg2Rad; // (float)Math.Atan(targetHeight / (2f * Position3D.Z)) * 2f;
            var aspectRatio = viewport.Y != 0 ? viewport.X / (float)viewport.Y : 0;
            return Matrix4x4.CreatePerspectiveFieldOfView(
                fov,
                aspectRatio,
                0.0001f,
                5000f
            );
        }
    }
    
    public Matrix4x4 ViewProjection3D => View3D * Projection3D;
    public Matrix4x4 ViewProjection => View * Projection;
}
