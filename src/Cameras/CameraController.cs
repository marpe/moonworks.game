using MyGame.Screens;
using MyGame.TWConsole;

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


    private Entity? _trackingEntity;

    private Velocity _velocity = new Velocity();

    [CVar("camera.trackspeed", "Camera tracking speed")]
    public static Vector2 TrackingSpeed = new Vector2(5f, 5f);
    
    private Vector2 _targetOffset = Vector2.Zero;

    public Vector2 DeadZoneInPercentOfViewport = new Vector2(0.04f, 0.1f);
    private float _baseFriction = 0.89f;
    private float _brakeDistNearBounds = 0.1f;
    private float bumpFrict = 0.85f;
    private Vector2 bumpOffset;
    private float _timer = 0;
    
    private float _shakeDuration = 0;
    private float _shakeTime = 0;
    private float _shakePower = 1.0f;
    public Vector2 TargetPosition = Vector2.Zero;
    
    private Vector2 _previousCameraPosition;
    private Matrix4x4 _viewProjection;
    private Matrix4x4 _previousViewProjection;

    public CameraController(GameScreen parent, Camera camera)
    {
        _parent = parent;
        _camera = camera;
        _viewProjection = _previousViewProjection = _camera.ViewProjection;
        _camera.Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
    }

    public void Update(float deltaSeconds, InputHandler input, bool allowMouseInput, bool allowKeyboardInput)
    {
        _camera.PreviousBounds = _camera.Bounds;
        _previousCameraPosition = _camera.Position;
        _previousViewProjection = _viewProjection;
        
        _timer += deltaSeconds;
        _lerpT = MathF.Clamp01(_lerpT + (Use3D ? 1 : -1) * deltaSeconds * _lerpSpeed);

        if (_trackingEntity != null)
        {
            var trackSpeed = TrackingSpeed * _camera.Zoom;
            TargetPosition = _trackingEntity.Center + _targetOffset;

            var offset = TargetPosition - _camera.Position;
            var angleToTarget = offset.Angle();
            var deadZone = DeadZoneInPercentOfViewport * _camera.Size;
            var distX = Math.Abs(offset.X);
            if (distX >= deadZone.X)
                _velocity.X += MathF.Cos(angleToTarget) * (0.8f * distX - deadZone.X) * trackSpeed.X * deltaSeconds;

            var distY = Math.Abs(offset.Y);
            if (distY >= deadZone.Y)
                _velocity.Y += MathF.Sin(angleToTarget) * (0.8f * distY - deadZone.Y) * trackSpeed.Y * deltaSeconds;
        }

        if (IsMouseAndKeyboardControlEnabled)
        {
            HandleInput(deltaSeconds, input, allowMouseInput, allowKeyboardInput);
        }

        _velocity.Friction = new Vector2(_baseFriction, _baseFriction);

        if (ClampToLevelBounds)
        {
            var worldSize = _parent.World?.WorldSize ?? new Point(512, 256);
            var cameraSize = new Vector2(_camera.Width, _camera.Height);
            var brakeDist = cameraSize * _brakeDistNearBounds;

            var left = MathF.Clamp01((_camera.Position.X - _camera.Width * 0.5f) / brakeDist.X);
            var right = MathF.Clamp01((worldSize.X - _camera.Width * 0.5f - _camera.Position.X) / brakeDist.X);
            var top = MathF.Clamp01((_camera.Position.Y - _camera.Height * 0.5f) / brakeDist.Y);
            var bottom = MathF.Clamp01((worldSize.Y - _camera.Height * 0.5f - _camera.Position.Y) / brakeDist.Y);

            if (_velocity.X < 0)
            {
                _velocity.Friction.X *= left;
            }
            else if (_velocity.X > 0)
            {
                _velocity.Friction.X *= right;
            }
            if (_velocity.Y < 0)
            {
                _velocity.Friction.Y *= top;
            }
            else if (_velocity.Y > 0)
            {
                _velocity.Friction.Y *= bottom;
            }
        }

        _camera.Position += _velocity * deltaSeconds;

        Velocity.ApplyFriction(_velocity);
        
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
            var percentDone = _shakeTime / _shakeDuration;
            var shakeOffset = new Vector2(MathF.Cos(0.0f + _timer * 1.1f), MathF.Sin(0.3f + _timer * 1.7f))
                              * percentDone * 2.5f * _shakePower;
            _camera.Position += shakeOffset;
        }

        bumpOffset *= Vector2.One * MathF.Pow(bumpFrict, deltaSeconds);
        _camera.Position += bumpOffset;
        _viewProjection = Matrix4x4.Lerp(_camera.ViewProjection, _camera.ViewProjection3D, Easing.InOutCubic(0, 1.0f, _lerpT, 1.0f));
    }

    public Matrix4x4 GetViewProjection(double alpha)
    {
        return Matrix4x4.Lerp(_previousViewProjection, _viewProjection, (float)alpha);
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
        _trackingEntity = target;
        if (target != null)
        {
            var targetPosition = target.Center + _targetOffset;
            _camera.Position = _previousCameraPosition = TargetPosition = targetPosition;
        }
    }
}
