using MyGame.WorldsRoot;

namespace MyGame.Entities;

public class Light : Entity
{
    [InstanceField, Range(0, 2)]
    public float Intensity = 0.45f;

    [InstanceField]
    public bool IsEnabled = true;

    [InstanceField]
    public Color Color = ColorExt.FromHex("FF9E31");

    [InstanceField, Range(0, 360)]
    public float Angle;

    [InstanceField, Range(0, 360)]
    public float ConeAngle = 360f;

    [InstanceField, Range(0, 1)]
    public float RimIntensity = 0.3f;

    [InstanceField, Range(0, 1)]
    public float VolumetricIntensity = 0.25f;
}
