namespace MyGame.Cameras;

public class Camera
{
    [Inspectable] protected Vector2 _cameraRotation = new(0, MathHelper.Pi);

    [CVar("camera.use3d", "Use 3D Camera")]
    public static bool Use3D;

    [CVar("noclip", "Toggle camera controls")]
    public static bool NoClip;

    private readonly float _lerpSpeed = 1f;
    private float _lerpT = 0;

    private float _zoom = 4.0f;
    public Vector2 BumpOffset;

    public Vector2 DeadZoneInPercentOfViewport = new(0.004f, 0.001f);

    public Vector2 Position;

    public Vector3 Position3D = new(0, 0, -1000);

    public float Rotation = 0;

    public Quaternion Rotation3D;
    public Vector2 ShakeOffset;
    public Vector2 TargetOffset;
    public Vector2 TargetPosition;

    private Vector2 _lastViewPosition;

    /// <summary>This is the "true" position, that was used for the view projection calculation Which has shake and bump and crap applied</summary>
    public Vector2 ViewPosition;

    public int Width => MathF.CeilToInt(Size.X / Zoom);
    public int Height => MathF.CeilToInt(Size.Y / Zoom);

    public Point Size = MyGameMain.DesignResolution;

    public Bounds Bounds => new(Position.X - Width / 2f, Position.Y - Height / 2f, Width, Height);

    public Entity? TrackingEntity;

    public Velocity Velocity = new()
    {
        Friction = new Vector2(0.9f, 0.9f),
    };

    public float Zoom
    {
        get => _zoom;
        set => _zoom = MathF.Clamp(value, 0.001f, 10f);
    }

    private Matrix3x2 GetView()
    {
        ViewPosition = Position + ShakeOffset + BumpOffset;
        return Matrix3x2.CreateTranslation(-ViewPosition.X, -ViewPosition.Y) *
               Matrix3x2.CreateRotation(Rotation) *
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
    public Vector2 InitialFriction;
    public Vector2 ShakeFequencies = new(50, 40);
    private float _freezeCameraTimer;
    private float _timer = 0;

    public float BrakeDistNearBounds = 0.1f;
    public float BumpFrict = 0.85f;
    private float _shakeDuration = 0;
    private float _shakePower = 4f;
    private float _shakeTime = 0;
    public Vector2 TrackingSpeed = new(5f, 5f);
    public bool ClampToLevelBounds;
    private GameScreen _gameScreen;

    private Point WorldSize => _gameScreen.World?.WorldSize ?? new Point(512, 256);

    public Camera(GameScreen gameScreen)
    {
        _gameScreen = gameScreen;
        InitialFriction = Velocity.Friction;
        Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
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
            var trackSpeed = TrackingSpeed * Zoom;
            TargetPosition = TrackingEntity.Center + TargetOffset;

            var offset = TargetPosition - Position;
            var angleToTarget = offset.Angle();
            var deadZone = DeadZoneInPercentOfViewport * Size;
            var distX = Math.Abs(offset.X);
            if (distX >= deadZone.X)
            {
                Velocity.X += MathF.Cos(angleToTarget) * (0.8f * distX - deadZone.X) * trackSpeed.X * deltaSeconds;
            }

            var distY = Math.Abs(offset.Y);
            if (distY >= deadZone.Y)
            {
                Velocity.Y += MathF.Sin(angleToTarget) * (0.8f * distY - deadZone.Y) * trackSpeed.Y * deltaSeconds;
            }
        }

        Velocity.Friction = InitialFriction;

        if (ClampToLevelBounds)
        {
            var cameraSize = new Vector2(Width, Height);
            var brakeDist = cameraSize * BrakeDistNearBounds;

            var left = MathF.Clamp01((Position.X - Width * 0.5f) / brakeDist.X);
            var right = MathF.Clamp01((WorldSize.X - Width * 0.5f - Position.X) / brakeDist.X);
            var top = MathF.Clamp01((Position.Y - Height * 0.5f) / brakeDist.Y);
            var bottom = MathF.Clamp01((WorldSize.Y - Height * 0.5f - Position.Y) / brakeDist.Y);

            if (Velocity.X < 0)
            {
                Velocity.Friction.X *= left;
            }
            else if (Velocity.X > 0)
            {
                Velocity.Friction.X *= right;
            }

            if (Velocity.Y < 0)
            {
                Velocity.Friction.Y *= top;
            }
            else if (Velocity.Y > 0)
            {
                Velocity.Friction.Y *= bottom;
            }
        }

        Position += Velocity * deltaSeconds;

        Velocity.ApplyFriction(Velocity);

        // Bounds clamping
        if (ClampToLevelBounds)
        {
            if (WorldSize.X < Width)
            {
                Position.X = WorldSize.X * 0.5f; // centered small level
            }
            else
            {
                Position.X = MathF.Clamp(Position.X, Width * 0.5f, WorldSize.X - Width * 0.5f);
            }

            if (WorldSize.Y < Height)
            {
                Position.Y = WorldSize.Y * 0.5f; // centered small level
            }
            else
            {
                Position.Y = MathF.Clamp(Position.Y, Height * 0.5f, WorldSize.Y - Height * 0.5f);
            }
        }

        if (_shakeTime > 0 && _shakeDuration > 0)
        {
            var percentDone = MathF.Clamp01(_shakeTime / _shakeDuration);
            _shakeTime -= deltaSeconds;
            ShakeOffset = new Vector2(
                MathF.Cos(0.0f + _timer * ShakeFequencies.X),
                MathF.Sin(0.3f + _timer * ShakeFequencies.X)
            ) * percentDone * _shakePower;
        }

        BumpOffset *= Vector2.One * MathF.Pow(BumpFrict, deltaSeconds);
    }

    private void HandleInput(float deltaSeconds, InputHandler input)
    {
        if (Use3D)
        {
            if (input.IsMouseButtonHeld(MouseButtonCode.Right))
            {
                var rotationSpeed = 0.1f;
                _cameraRotation += new Vector2(input.MouseDelta.X, -input.MouseDelta.Y) * rotationSpeed * deltaSeconds;
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                Rotation3D = rotation;
            }

            if (input.IsKeyPressed(KeyCode.Home))
            {
                _cameraRotation = new Vector2(0, MathHelper.Pi);
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                Rotation3D = rotation;
                Position3D = new Vector3(0, 0, -1000);
            }

            var camera3DSpeed = 750f;
            var moveDelta = camera3DSpeed * deltaSeconds;
            if (input.IsKeyDown(KeyCode.W))
            {
                Position3D += Vector3.Transform(Vector3.Forward, Rotation3D) * moveDelta;
            }

            if (input.IsKeyDown(KeyCode.S))
            {
                Position3D -= Vector3.Transform(Vector3.Forward, Rotation3D) * moveDelta;
            }

            if (input.IsKeyDown(KeyCode.A))
            {
                Position3D += Vector3.Transform(Vector3.Left, Rotation3D) * moveDelta;
            }

            if (input.IsKeyDown(KeyCode.D))
            {
                Position3D += Vector3.Transform(Vector3.Right, Rotation3D) * moveDelta;
            }
        }
        else
        {
            if (input.MouseWheelDelta != 0)
            {
                Zoom += 0.1f * Zoom * input.MouseWheelDelta;
            }

            var cameraSpeed = 500f;

            if (input.IsMouseButtonHeld(MouseButtonCode.Right))
            {
                Position += new Vector2(input.MouseDelta.X, input.MouseDelta.Y) * 50 * deltaSeconds;
            }

            var moveDelta = cameraSpeed * deltaSeconds;

            if (input.IsKeyPressed(KeyCode.Home))
            {
                Zoom = 1.0f;
                Position = Vector2.Zero;
            }

            if (input.IsKeyDown(KeyCode.PageUp))
            {
                Zoom += 0.025f * Zoom;
            }

            if (input.IsKeyDown(KeyCode.PageDown))
            {
                Zoom -= 0.025f * Zoom;
            }

            if (input.IsKeyDown(KeyCode.W))
            {
                Position.Y -= moveDelta;
            }

            if (input.IsKeyDown(KeyCode.S))
            {
                Position.Y += moveDelta;
            }

            if (input.IsKeyDown(KeyCode.A))
            {
                Position.X -= moveDelta;
            }

            if (input.IsKeyDown(KeyCode.D))
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
