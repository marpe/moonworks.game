namespace MyGame.Cameras;

public static class CameraBinds
{
    public static ButtonBind ZoomIn = new();
    public static ButtonBind ZoomOut = new();
    public static ButtonBind Up = new();
    public static ButtonBind Down = new();
    public static ButtonBind Forward = new();
    public static ButtonBind Back = new();
    public static ButtonBind Right = new();
    public static ButtonBind Left = new();
    public static ButtonBind Pan = new();
    public static ButtonBind Reset = new();
}

public class Camera
{
    private Vector2 _cameraRotation3D = new(0, MathHelper.Pi);

    [CVar("cam.use3d", "Use 3D Camera")]
    public static bool Use3D;

    [CVar("noclip", "Toggle camera controls")]
    public static bool NoClip;

    // this is just here as a convenience so it can be toggled from an imgui inspector
    public bool UseRelativeMouseMode
    {
        get => Shared.Game.Inputs.Mouse.RelativeMode;
        set => Shared.Game.Inputs.Mouse.RelativeMode = value;
    }

    private readonly float _lerpSpeed = 1f;
    private float _lerpT = 0;

    private float _zoom = 1.0f;
    public Vector2 BumpOffset;

    public Vector2 DeadZoneInPercentOfViewport = new(0.2f, 0.2f);

    public Vector2 DeadZone => DeadZoneInPercentOfViewport * ZoomedSize;

    public Vector2 BrakeZone => BrakeZoneInPercentOfViewport * ZoomedSize;
    public float BrakeZoneInPercentOfViewport = 0.2f;

    public Vector2 Position;

    public Vector3 Position3D = new(0, 0, -1000);

    public float RotationDegrees = 0;

    public Quaternion Rotation3D;
    public Vector2 ShakeOffset;
    public Vector2 TargetOffset;
    public Vector2 TargetPosition;

    /// <summary>This is the "true" position, that was used for the view projection calculation Which has shake and bump and crap applied</summary>
    public Vector2 ViewPosition;


    public UPoint Size;

    public Vector2 ZoomedSize => Size.ToVec2() / Zoom;

    public Bounds ZoomedBounds
    {
        get
        {
            var size = ZoomedSize;
            var bounds = new Bounds(
                Position.X - size.X / 2f,
                Position.Y - size.Y / 2f,
                size.X,
                size.Y
            );
            return bounds;
        }
    }

    public Entity? TrackingEntity;

    public Velocity Velocity = new()
    {
        Friction = new Vector2(0.9f, 0.9f),
    };

    public float Zoom
    {
        get => _zoom;
        set => _zoom = MathF.Clamp(value, 0.001f, 50f);
    }

    public bool FloorViewPosition;
    public Vector2 FloorRemainder => ViewPosition - FlooredViewPosition;
    public Vector2 FlooredViewPosition => ViewPosition.Floor();

    public Matrix3x2 GetView()
    {
        ViewPosition = Position + ShakeOffset + BumpOffset;
        var position = FloorViewPosition ? FlooredViewPosition : ViewPosition;
        return Matrix3x2.CreateTranslation(-position.X, -position.Y) *
               Matrix3x2.CreateRotation(RotationDegrees * MathF.Deg2Rad) *
               Matrix3x2.CreateScale(_zoom) *
               Matrix3x2.CreateTranslation(Size.X * 0.5f, Size.Y * 0.5f);
    }

    private Matrix4x4 GetView3D()
    {
        var position = new Vector3(Position3D.X, Position3D.Y, Position3D.Z);
        return Matrix4x4.CreateLookAt(
            position,
            position + Vector3.Transform(Vector3.Forward, Rotation3D),
            Vector3.Down
        );
    }

    public int HorizontalFovDegrees = 60;
    private Vector2 _initialFriction;
    public Vector2 ShakeFrequencies = new(50, 40);
    private float _freezeCameraTimer;
    private float _timer = 0;

    private float _bumpFriction = 0.85f;
    private float _shakeDuration = 0;
    private float _shakePower = 4f;
    private float _shakeTime = 0;
    private Vector2 _trackingSpeed = new(5f, 5f);
    public bool ClampToLevelBounds;

    public Rectangle LevelBounds = Rectangle.Empty;

    [CVar("cam.3d_sens", "Mouse sensitivity when camera is in 3D mode")]
    public static float MouseSensitivity3D = 1.0f;

    [CVar("cam.3d_speed", "Camera movement speed when camera is in 3D mode")]
    public static float Camera3DSpeed = 750f;

    private float _cameraSpeed = 500f;
    private float _cameraMouseSensitivity = 50f;

    public Camera(uint width, uint height)
    {
        _initialFriction = Velocity.Friction;
        Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation3D.X, _cameraRotation3D.Y, 0);

        Size = new UPoint(width, height);
        Zoom = GetZoomFromRenderScale();
        FloorViewPosition = MyGameMain.RenderScale > 1; // only floor when rendering at a larger scale than 1
    }

    private static float GetZoomFromRenderScale()
    {
        return 5 - MyGameMain.RenderScale; // should be 1 when rendering at 480x270 and 4 when rendering at 1920x1080
    }

    public void Update(float deltaSeconds, InputHandler input)
    {
        _timer += deltaSeconds;
        _lerpT = MathF.Clamp01(_lerpT + (Use3D ? 1 : -1) * deltaSeconds * _lerpSpeed);

        if (NoClip)
        {
            HandleInput(deltaSeconds, input);
            return;
        }

        if (_freezeCameraTimer > 0)
        {
            _freezeCameraTimer -= deltaSeconds;
            return;
        }

        _freezeCameraTimer = 0;

        if (TrackingEntity != null)
        {
            var trackSpeed = _trackingSpeed * Zoom;
            TargetPosition = TrackingEntity.Center + TargetOffset;

            var offset = TargetPosition - Position;
            var direction = offset.ToNormal();
            var deadZone = DeadZone / 2; // divide by 2 since position is the center of the camera
            var distX = Math.Abs(offset.X) - deadZone.X;
            if (distX > 0)
            {
                Velocity.X += direction.X * distX * trackSpeed.X * deltaSeconds;
            }

            var distY = Math.Abs(offset.Y) - deadZone.Y;
            if (distY > 0)
            {
                Velocity.Y += direction.Y * distY * trackSpeed.Y * deltaSeconds;
            }
        }

        Velocity.Friction = _initialFriction;

        if (ClampToLevelBounds && !LevelBounds.IsEmpty)
        {
            var brakeZone = BrakeZone;
            var offset = Position - LevelBounds.MinVec();

            var cameraMin = offset - ZoomedSize * 0.5f;
            var cameraMax = offset + ZoomedSize * 0.5f;

            var brakePower = 0.9f;
            if (Velocity.X < 0 && cameraMin.X < brakeZone.X)
            {
                var left = (brakeZone.X - cameraMin.X) / brakeZone.X;
                Velocity.Friction.X *= (1.0f - left * brakePower);
            }
            else if (Velocity.X > 0 && cameraMax.X > LevelBounds.Width - brakeZone.X)
            {
                var right = (cameraMax.X - (LevelBounds.Width - brakeZone.X)) / brakeZone.X;
                Velocity.Friction.X *= (1.0f - right * brakePower);
            }

            if (Velocity.Y < 0 && cameraMin.Y < brakeZone.Y)
            {
                var top = (brakeZone.Y - cameraMin.Y) / brakeZone.Y;
                Velocity.Friction.Y *= (1.0f - top * brakePower);
            }
            else if (Velocity.Y > 0 && cameraMax.Y > LevelBounds.Height - brakeZone.Y)
            {
                var bottom = (cameraMax.Y - (LevelBounds.Height - brakeZone.Y)) / brakeZone.Y;
                Velocity.Friction.Y *= (1.0f - bottom * brakePower);
            }
        }

        Position += Velocity * deltaSeconds;

        Velocity.ApplyFriction(Velocity);

        if (ClampToLevelBounds && !LevelBounds.IsEmpty)
        {
            var cameraSize = ZoomedSize;
            if (LevelBounds.Width < cameraSize.X)
            {
                // if the level width is less than the camera width, center the camera
                Position.X = LevelBounds.X + LevelBounds.Width * 0.5f;
            }
            else
            {
                Position.X = MathF.Clamp(
                    Position.X,
                    LevelBounds.X + cameraSize.X * 0.5f,
                    LevelBounds.X + LevelBounds.Width - cameraSize.X * 0.5f
                );
            }

            if (LevelBounds.Height < cameraSize.Y)
            {
                // if the level height is less than the camera height, center the camera
                Position.Y = LevelBounds.Y + LevelBounds.Height * 0.5f;
            }
            else
            {
                Position.Y = MathF.Clamp(
                    Position.Y,
                    LevelBounds.Y + cameraSize.Y * 0.5f,
                    LevelBounds.Y + LevelBounds.Height - cameraSize.Y * 0.5f
                );
            }
        }

        ShakeOffset = Vector2.Zero;
        if (_shakeTime > 0 && _shakeDuration > 0)
        {
            var percentDone = MathF.Clamp01(_shakeTime / _shakeDuration);
            _shakeTime -= deltaSeconds;
            ShakeOffset = new Vector2(
                MathF.Cos(0.0f + _timer * ShakeFrequencies.X),
                MathF.Sin(0.3f + _timer * ShakeFrequencies.X)
            ) * percentDone * _shakePower;
        }

        BumpOffset *= Vector2.One * MathF.Pow(_bumpFriction, deltaSeconds);
    }

    private void HandleInput(float deltaSeconds, InputHandler input)
    {
        if (Use3D)
        {
            if (CameraBinds.Pan.Active || Shared.Game.Inputs.Mouse.RelativeMode)
            {
                var rotationSpeed = 0.1f * MouseSensitivity3D;
                _cameraRotation3D += new Vector2(input.MouseDelta.X, -input.MouseDelta.Y) * rotationSpeed * deltaSeconds;
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation3D.X, _cameraRotation3D.Y, 0);
                Rotation3D = rotation;
            }

            if (CameraBinds.Reset.Active)
            {
                _cameraRotation3D = new Vector2(0, MathHelper.Pi);
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation3D.X, _cameraRotation3D.Y, 0);
                Rotation3D = rotation;
                Position3D = new Vector3(0, 0, -1000);
            }

            var moveDelta = Camera3DSpeed * deltaSeconds;
            if (CameraBinds.Up.Active)
            {
                Position3D += Vector3.Transform(Vector3.Up, Rotation3D) * moveDelta;
            }

            if (CameraBinds.Down.Active)
            {
                Position3D += Vector3.Transform(Vector3.Down, Rotation3D) * moveDelta;
            }

            if (CameraBinds.Forward.Active)
            {
                Position3D += Vector3.Transform(Vector3.Forward, Rotation3D) * moveDelta;
            }

            if (CameraBinds.Back.Active)
            {
                Position3D += Vector3.Transform(Vector3.Backward, Rotation3D) * moveDelta;
            }

            if (CameraBinds.Left.Active)
            {
                Position3D += Vector3.Transform(Vector3.Left, Rotation3D) * moveDelta;
            }

            if (CameraBinds.Right.Active)
            {
                Position3D += Vector3.Transform(Vector3.Right, Rotation3D) * moveDelta;
            }
        }
        else
        {
            if (CameraBinds.Pan.Active)
            {
                Position -= new Vector2(input.MouseDelta.X, input.MouseDelta.Y) * _cameraMouseSensitivity * deltaSeconds;
            }

            var moveDelta = _cameraSpeed * deltaSeconds;

            if (CameraBinds.Reset.Active)
            {
                Zoom = GetZoomFromRenderScale();
                TargetPosition = TrackingEntity != null ? TrackingEntity.Center + TargetOffset : TargetPosition;
                Position = TargetPosition;
            }

            if (CameraBinds.ZoomIn.Active)
            {
                Zoom += 0.025f * Zoom;
            }

            if (CameraBinds.ZoomOut.Active)
            {
                Zoom -= 0.025f * Zoom;
            }

            if (CameraBinds.Forward.Active)
            {
                Position.Y -= moveDelta;
            }

            if (CameraBinds.Back.Active)
            {
                Position.Y += moveDelta;
            }

            if (CameraBinds.Left.Active)
            {
                Position.X -= moveDelta;
            }

            if (CameraBinds.Right.Active)
            {
                Position.X += moveDelta;
            }
        }

        input.KeyboardEnabled = input.MouseEnabled = false;
    }

    [InspectorCallable]
    public void SetShake(float shakeDuration, float shakePower)
    {
        _shakeTime = _shakeDuration = shakeDuration;
        _shakePower = shakePower;
    }

    public void TrackEntity(Entity? target)
    {
        TrackingEntity = target;
        if (target != null)
        {
            var targetPosition = target.Center + TargetOffset;
            Position = TargetPosition = targetPosition;
        }
    }

    private Matrix4x4 GetProjection(uint width, uint height)
    {
        return Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, 0.0001f, 10000f);
    }

    private Matrix4x4 GetProjection3D(uint width, uint height)
    {
        var aspectRatio = width / (float)height;
        var hFov = HorizontalFovDegrees * MathF.Deg2Rad;
        var vFov = MathF.Atan(MathF.Tan(hFov / 2.0f) / aspectRatio) * 2.0f;
        return Matrix4x4.CreatePerspectiveFieldOfView(vFov, aspectRatio, 0.0001f, 10000f);
    }

    public Matrix4x4 GetViewProjection(uint width, uint height)
    {
        var cameraView = GetView().ToMatrix4x4();
        cameraView.M43 = -1000;
        var cameraView3D = GetView3D();

        var projection = GetProjection(width, height);
        var projection3D = GetProjection3D(width, height);

        return Matrix4x4.Lerp(cameraView * projection, cameraView3D * projection3D, Easing.InOutCubic(0, 1.0f, _lerpT, 1.0f));
    }
}
