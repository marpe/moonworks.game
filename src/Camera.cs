namespace MyGame;

public class Camera
{
    public int Width = 1280;

    public int Height = 720;

    public Point Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.X;
            Height = value.Y;
        }
    }

    public Vector2 Position;

    public Vector3 Position3D = new(0, 0, -1000);

    private Matrix4x4 View => Matrix4x4.CreateLookAt(
        new Vector3(Position.X, Position.Y, 1000),
        new Vector3(Position.X, Position.Y, 0),
        Vector3.Up
    );

    private Matrix4x4 View3D
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

    public Matrix4x4 Projection => Matrix4x4.CreateOrthographicOffCenter(
        0,
        Width,
        Height,
        0,
        0.0001f,
        4000f
    );

    public Matrix4x4 Projection3D
    {
        get
        {
            var targetHeight = Height; // / zoom
            var fov = 60 * MathF.Deg2Rad; // (float)Math.Atan(targetHeight / (2f * Position3D.Z)) * 2f;
            var aspectRatio = Height != 0 ? Width / (float)Height : 0;
            return Matrix4x4.CreatePerspectiveFieldOfView(
                fov,
                aspectRatio,
                0.0001f,
                5000f
            );
        }
    }

    public bool Use3D;
    
    public Matrix4x4 ViewProjectionMatrix => Use3D ? View3D * Projection3D : View * Projection;
}
