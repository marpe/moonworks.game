namespace MyGame.Cameras;

public class CameraController
{
    private Components.Camera _camera;
    private Vector2 _cameraRotation = new Vector2(0, MathHelper.Pi);

    public CameraController(Components.Camera camera)
    {
        _camera = camera;
        _camera.Rotation3D = Quaternion.CreateFromYawPitchRoll(_cameraRotation.X, _cameraRotation.Y, 0);
    }

    public void Update(float deltaSeconds, InputHandler input, bool allowMouseInput, bool allowKeyboardInput)
    {
        if (allowKeyboardInput && input.IsKeyPressed(KeyCode.F1))
        {
            _camera.Use3D = !_camera.Use3D;
        }

        if (_camera.Use3D)
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
            if (allowKeyboardInput)
            {
                var cameraSpeed = 500f;
                var moveDelta = cameraSpeed * deltaSeconds;

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
}
