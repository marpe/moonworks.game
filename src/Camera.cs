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

    private Matrix4x4 View => Matrix4x4.CreateLookAt(
        new Vector3(0, 0, 1),
        Vector3.Zero,
        Vector3.Up
    );

    public Matrix4x4 Projection => Matrix4x4.CreateOrthographicOffCenter(
        0,
        Width,
        Height,
        0,
        0.01f,
        4000f
    );

    public Matrix4x4 ViewProjectionMatrix => View * Projection;
}
