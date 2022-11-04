using MyGame.Screens;
using MyGame.TWConsole;
using MyGame.TWImGui;

namespace MyGame.Cameras;

public class CameraController
{
    private GameScreen _parent;
    private Camera _camera;
    private Vector2 _cameraRotation = new Vector2(0, MathHelper.Pi);
    public bool Use3D;
    private float _lerpT = 0;
    private float _lerpSpeed = 1f;

    [CVar("camera.input", "Toggle camera controls")]
    public static bool IsMouseAndKeyboardControlEnabled;

    [CVar("camera.clamp", "Toggle clamping of camera to level bounds")]
    public static bool ClampToLevelBounds;

    public Entity? TrackingEntity;

    public Velocity Velocity = new Velocity()
    {
        Friction = new Vector2(0.9f, 0.9f)
    };

    public Vector2 InitialFriction;

    [CVar("camera.trackspeed", "Camera tracking speed")]
    public static Vector2 TrackingSpeed = new Vector2(5f, 5f);

    private float _timer = 0;

    public float BrakeDistNearBounds = 0.1f;
    public float BumpFrict = 0.85f;

    private float _shakeDuration = 0;
    private float _shakeTime = 0;
    private float _shakePower = 4f;

    private Matrix4x4 _viewProjection;
    private Matrix4x4 _previousViewProjection;
    public Vector2 ShakeFequencies = new Vector2(50, 40);

    public CameraController(GameScreen parent, Camera camera)
    {
        InitialFriction = Velocity.Friction;

        _parent = parent;
        _camera = camera;
        _viewProjection = _previousViewProjection = _camera.ViewProjection;
        _camera.Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
    }

    private void UpdatePrevious()
    {
        _camera.PreviousBounds = _camera.Bounds;
        _previousViewProjection = _viewProjection;
    }

    public void Update(bool isPaused, float deltaSeconds, InputHandler input, bool allowMouseInput, bool allowKeyboardInput)
    {
        UpdatePrevious();
        if (isPaused)
            return;

        _timer += deltaSeconds;
        _lerpT = MathF.Clamp01(_lerpT + (Use3D ? 1 : -1) * deltaSeconds * _lerpSpeed);

        if (TrackingEntity != null)
        {
            var trackSpeed = TrackingSpeed * _camera.Zoom;
            _camera.TargetPosition = TrackingEntity.Center + _camera.TargetOffset;

            var offset = _camera.TargetPosition - _camera.Position;
            var angleToTarget = offset.Angle();
            var deadZone = _camera.DeadZoneInPercentOfViewport * _camera.Size;
            var distX = Math.Abs(offset.X);
            if (distX >= deadZone.X)
                Velocity.X += MathF.Cos(angleToTarget) * (0.8f * distX - deadZone.X) * trackSpeed.X * deltaSeconds;

            var distY = Math.Abs(offset.Y);
            if (distY >= deadZone.Y)
                Velocity.Y += MathF.Sin(angleToTarget) * (0.8f * distY - deadZone.Y) * trackSpeed.Y * deltaSeconds;
        }

        if (IsMouseAndKeyboardControlEnabled)
        {
            HandleInput(deltaSeconds, input, allowMouseInput, allowKeyboardInput);
        }

        Velocity.Friction = InitialFriction;

        if (ClampToLevelBounds)
        {
            var worldSize = _parent.World?.WorldSize ?? new Point(512, 256);
            var cameraSize = new Vector2(_camera.Width, _camera.Height);
            var brakeDist = cameraSize * BrakeDistNearBounds;

            var left = MathF.Clamp01((_camera.Position.X - _camera.Width * 0.5f) / brakeDist.X);
            var right = MathF.Clamp01((worldSize.X - _camera.Width * 0.5f - _camera.Position.X) / brakeDist.X);
            var top = MathF.Clamp01((_camera.Position.Y - _camera.Height * 0.5f) / brakeDist.Y);
            var bottom = MathF.Clamp01((worldSize.Y - _camera.Height * 0.5f - _camera.Position.Y) / brakeDist.Y);

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

        _camera.Position += Velocity * deltaSeconds;

        Velocity.ApplyFriction(Velocity);

        // Bounds clamping
        if (ClampToLevelBounds)
        {
            var worldSize = _parent.World?.WorldSize ?? new Point(512, 256);

            if (worldSize.X < _camera.Width)
                _camera.Position.X = worldSize.X * 0.5f; // centered small level
            else
                _camera.Position.X = MathF.Clamp(_camera.Position.X, _camera.Width * 0.5f, worldSize.X - _camera.Width * 0.5f);

            if (worldSize.Y < _camera.Height)
                _camera.Position.Y = worldSize.Y * 0.5f; // centered small level
            else
                _camera.Position.Y = MathF.Clamp(_camera.Position.Y, _camera.Height * 0.5f, worldSize.Y - _camera.Height * 0.5f);
        }

        if (_shakeTime > 0 && _shakeDuration > 0)
        {
            var percentDone = MathF.Clamp01(_shakeTime / _shakeDuration);
            _shakeTime -= deltaSeconds;
            _camera.ShakeOffset = new Vector2(
                MathF.Cos(0.0f + _timer * ShakeFequencies.X),
                MathF.Sin(0.3f + _timer * ShakeFequencies.X)
            ) * percentDone * _shakePower;
        }

        _camera.BumpOffset *= Vector2.One * MathF.Pow(BumpFrict, deltaSeconds);

        _viewProjection = Matrix4x4.Lerp(_camera.ViewProjection, _camera.ViewProjection3D, Easing.InOutCubic(0, 1.0f, _lerpT, 1.0f));
    }

    public Matrix4x4 GetViewProjection(double alpha)
    {
        return Matrix4x4.Lerp(_previousViewProjection, _viewProjection, (float)alpha);
    }

    [InspectorCallable]
    public void SetShake(float shakeDuration, float shakePower)
    {
        _shakeTime = _shakeDuration = shakeDuration;
        _shakePower = shakePower;
    }

    private void HandleInput(float deltaSeconds, InputHandler input, bool allowMouseInput, bool allowKeyboardInput)
    {
        if (allowKeyboardInput && input.IsKeyPressed(KeyCode.F1))
        {
            Use3D = !Use3D;
        }

        if (Use3D)
        {
            if (allowMouseInput && input.IsMouseButtonHeld(MouseButtonCode.Right))
            {
                var rotationSpeed = 0.1f;
                _cameraRotation += new Vector2(input.MouseDelta.X, -input.MouseDelta.Y) * rotationSpeed * deltaSeconds;
                var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                _camera.Rotation3D = rotation;
            }

            if (allowKeyboardInput)
            {
                if (input.IsKeyPressed(KeyCode.Home))
                {
                    _cameraRotation = new Vector2(0, MathHelper.Pi);
                    var rotation = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
                    _camera.Rotation3D = rotation;
                    _camera.Position3D = new Vector3(0, 0, -1000);
                }

                var camera3DSpeed = 750f;
                var moveDelta = camera3DSpeed * deltaSeconds;
                if (input.IsKeyDown(KeyCode.W))
                {
                    _camera.Position3D += Vector3.Transform(Vector3.Forward, _camera.Rotation3D) * moveDelta;
                }

                if (input.IsKeyDown(KeyCode.S))
                {
                    _camera.Position3D -= Vector3.Transform(Vector3.Forward, _camera.Rotation3D) * moveDelta;
                }

                if (input.IsKeyDown(KeyCode.A))
                {
                    _camera.Position3D += Vector3.Transform(Vector3.Left, _camera.Rotation3D) * moveDelta;
                }

                if (input.IsKeyDown(KeyCode.D))
                {
                    _camera.Position3D += Vector3.Transform(Vector3.Right, _camera.Rotation3D) * moveDelta;
                }
            }
        }
        else
        {
            if (allowMouseInput)
            {
                if (input.MouseWheelDelta != 0)
                {
                    _camera.Zoom += 0.1f * _camera.Zoom * input.MouseWheelDelta;
                }
            }

            if (allowKeyboardInput)
            {
                var cameraSpeed = 500f;
                var moveDelta = cameraSpeed * deltaSeconds;

                if (input.IsKeyPressed(KeyCode.Home))
                {
                    _camera.Zoom = 1.0f;
                    _camera.Position = Vector2.Zero;
                }

                if (input.IsKeyDown(KeyCode.PageUp))
                {
                    _camera.Zoom += 0.025f * _camera.Zoom;
                }

                if (input.IsKeyDown(KeyCode.PageDown))
                {
                    _camera.Zoom -= 0.025f * _camera.Zoom;
                }

                if (input.IsKeyDown(KeyCode.W))
                {
                    _camera.Position.Y -= moveDelta;
                }

                if (input.IsKeyDown(KeyCode.S))
                {
                    _camera.Position.Y += moveDelta;
                }

                if (input.IsKeyDown(KeyCode.A))
                {
                    _camera.Position.X -= moveDelta;
                }

                if (input.IsKeyDown(KeyCode.D))
                {
                    _camera.Position.X += moveDelta;
                }
            }
        }
    }

    public void TrackEntity(Entity? target)
    {
        TrackingEntity = target;
        if (target != null)
        {
            var targetPosition = target.Center + _camera.TargetOffset;
            _camera.Position = _camera.TargetPosition = targetPosition;
        }
    }
}
