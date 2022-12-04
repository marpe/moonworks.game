namespace MyGame.Input;

public class ButtonBind
{
    public string[] Sources = { "", "" };
    public float Timestamp;
    public float TimeHeld;
    public bool Active;
    public bool WasActive;
    public ulong Frame;
    public bool WasPressed => Active && !WasActive && Frame == Shared.Game.Time.UpdateCount;
}

public class InputHandler
{
    /// <summary>Number of seconds between each registered keypress when a key is being held down</summary>
    public const float REPEAT_DELAY = 0.03f;

    /// <summary>Number of seconds a key can be held down before being repeated</summary>
    public const float INITIAL_REPEAT_DELAY = 0.5f;

    public static readonly KeyCode[] ModifierKeys =
    {
        KeyCode.LeftControl,
        KeyCode.RightControl,
        KeyCode.LeftShift,
        KeyCode.RightShift,
        KeyCode.LeftAlt,
        KeyCode.RightAlt,
        KeyCode.LeftMeta,
        KeyCode.RightMeta,
    };

    public static readonly KeyCode[] ControlKeys = { KeyCode.LeftControl, KeyCode.RightControl };
    public static readonly KeyCode[] ShiftKeys = { KeyCode.LeftShift, KeyCode.RightShift };
    public static readonly KeyCode[] AltKeys = { KeyCode.LeftAlt, KeyCode.RightAlt };
    public static readonly KeyCode[] MetaKeys = { KeyCode.LeftMeta, KeyCode.RightMeta };

    private static readonly Dictionary<KeyCode, char> TextInputKeys = new()
    {
        { KeyCode.Home, (char)2 },
        { KeyCode.End, (char)3 },
        { KeyCode.Backspace, (char)8 },
        { KeyCode.Tab, (char)9 },
        { KeyCode.Return, (char)13 },
        { KeyCode.Delete, (char)127 },
    };

    public static readonly char ControlV = (char)22;

    private readonly Inputs _inputs;

    private readonly Dictionary<KeyCode, RepeatableKey> _repeatableKeys = new();

    private char[] _textInput = new char[128];
    private int _numTextInputChars;

    public bool KeyboardEnabled = true;
    public bool MouseEnabled = true;
    private Matrix4x4 _viewportInvert = Matrix4x4.Identity;

    public record struct CheckBindCallbacks(
        Func<int, string> GetButtonName,
        Func<int, bool> IsButtonDown,
        Func<int, bool> IsButtonReleased,
        Func<int, bool> IsButtonPressed
    );

    private CheckBindCallbacks _mouseCallbacks;
    private CheckBindCallbacks _mouseWheelCallbacks;
    private CheckBindCallbacks _keyboardCallbacks;

    public InputHandler(Inputs inputs)
    {
        _inputs = inputs;
        Inputs.TextInput += OnTextInput;

        _mouseCallbacks = new CheckBindCallbacks(
            buttonId => Binds.MouseButtonCodeToName[(MouseButtonCode)buttonId],
            buttonId => IsMouseButtonDown((MouseButtonCode)buttonId),
            buttonId => IsMouseButtonReleased((MouseButtonCode)buttonId),
            buttonId => IsMouseButtonPressed((MouseButtonCode)buttonId)
        );

        _mouseWheelCallbacks = new CheckBindCallbacks(
            buttonId => Binds.MouseWheelCodeToName[buttonId],
            buttonId => buttonId == Binds.MouseWheelUp ? MouseWheelDelta > 0 : MouseWheelDelta < 0,
            buttonId => MouseWheelDelta == 0,
            buttonId => buttonId == Binds.MouseWheelUp ? MouseWheelDelta > 0 : MouseWheelDelta < 0
        );

        _keyboardCallbacks = new CheckBindCallbacks(
            buttonId => Binds.KeyCodeToName[(KeyCode)buttonId],
            buttonId => IsKeyDown((KeyCode)buttonId),
            buttonId => IsKeyReleased((KeyCode)buttonId),
            buttonId => IsKeyPressed((KeyCode)buttonId)
        );
    }

    public Point MouseDelta => new(_inputs.Mouse.DeltaX, _inputs.Mouse.DeltaY);

    public Point MousePosition
    {
        get
        {
            var mousePosition = new Vector2(_inputs.Mouse.X, _inputs.Mouse.Y);
            Vector2.Transform(ref mousePosition, ref _viewportInvert, out var mouseInViewport);
            return mouseInViewport.ToPoint();
        }
    }

    public Point MousePositionRaw => new Point(_inputs.Mouse.X, _inputs.Mouse.Y);

    public int MouseWheelDelta => MouseEnabled ? _inputs.Mouse.Wheel : 0;

    /// <summary>https://github.com/FNA-XNA/FNA/wiki/5:-FNA-Extensions#textinputext</summary>
    /// <param name="c"></param>
    private void OnTextInput(char c)
    {
        if (_numTextInputChars >= _textInput.Length)
            Array.Resize(ref _textInput, _textInput.Length * 2);
        _textInput[_numTextInputChars] = c;
        _numTextInputChars += 1;
    }

    public void BeginFrame(float deltaSeconds)
    {
        foreach (var (keyCode, key) in _repeatableKeys)
        {
            var isHeld = _inputs.Keyboard.IsHeld(keyCode);
            key.Update(isHeld, deltaSeconds);
        }

        HandleBoundKeys();
    }

    private void HandleBoundKeys()
    {
        foreach (var (code, _) in Binds.MouseWheelCodeToName)
        {
            HandleButtonBind(code, _mouseWheelCallbacks);
        }

        foreach (var (code, _) in Binds.MouseButtonCodeToName)
        {
            HandleButtonBind((int)code, _mouseCallbacks);
        }

        foreach (var (code, _) in Binds.KeyCodeToName)
        {
            // console key handled separately
            if (code == KeyCode.Grave && IsKeyPressed(code))
            {
                ConsoleScreen.ToggleConsole();
                continue;
            }

            HandleButtonBind((int)code, _keyboardCallbacks);
        }
    }

    // TODO (marpe): Cleanup and optimize
    public void HandleButtonBind(int buttonId, in CheckBindCallbacks callbacks)
    {
        var keyStr = callbacks.GetButtonName(buttonId);
        if (!callbacks.IsButtonDown(buttonId))
        {
            if (!callbacks.IsButtonReleased(buttonId))
                return;

            if (!Binds.TryGetBind(keyStr, out var bind))
                return;

            var split = ConsoleUtils.SplitArgs(bind);
            var cmdKey = split[0];

            if (!cmdKey.StartsWith('+'))
                return;

            var upCmdKey = $"-{cmdKey.AsSpan().Slice(1)}";
            if (Shared.Console.Commands.ContainsKey(upCmdKey))
            {
                Shared.Console.ExecuteCommand(Shared.Console.Commands[upCmdKey], new[] { cmdKey, keyStr });
            }
            else
            {
                Logs.LogError($"Command not found: {keyStr} -> {cmdKey}");
            }
        }
        else
        {
            if (!callbacks.IsButtonPressed(buttonId))
                return;

            // ignore if console is down
            if (!Shared.Game.ConsoleScreen.IsHidden)
                return;

            if (!Binds.TryGetBind(keyStr, out var bind))
                return;

            var split = ConsoleUtils.SplitArgs(bind);
            var cmdKey = split[0];

            if (Shared.Console.CVars.ContainsKey(cmdKey))
            {
                var cvar = Shared.Console.CVars[cmdKey];
                if (cvar.VarType == typeof(bool))
                {
                    var curr = cvar.GetValue<bool>();
                    Shared.Console.ExecuteCommand(Shared.Console.Commands[cmdKey], new[] { cmdKey, (!curr).ToString() });
                    return;
                }
            }

            if (Shared.Console.Commands.ContainsKey(cmdKey))
            {
                if (cmdKey.StartsWith('+') || cmdKey.StartsWith('-'))
                {
                    Shared.Console.ExecuteCommand(Shared.Console.Commands[cmdKey], new[] { cmdKey, buttonId.ToString() });
                }
                else
                {
                    Shared.Console.ExecuteCommand(Shared.Console.Commands[cmdKey], split);
                }
            }
            else
            {
                Logs.LogError($"Command not found: {keyStr} -> {cmdKey}");
            }
        }
    }

    public void EndFrame()
    {
        _numTextInputChars = 0;
        KeyboardEnabled = MouseEnabled = true;
    }

    public bool IsAnyModifierKeyDown()
    {
        return !KeyboardEnabled && _inputs.Keyboard.IsAnyKeyDown(ModifierKeys);
    }

    public bool IsKeyPressed(KeyCode key, bool allowRepeating = false)
    {
        if (!KeyboardEnabled)
            return false;

        var isPressed = _inputs.Keyboard.IsPressed(key);

        if (allowRepeating)
        {
            if (!_repeatableKeys.ContainsKey(key))
            {
                _repeatableKeys.Add(key, new RepeatableKey());
            }

            isPressed |= _repeatableKeys[key].WasRepeated;
        }

        return isPressed;
    }

    public bool IsAnyKeyPressed(bool allowRepeating = false)
    {
        if (!KeyboardEnabled)
            return false;

        var isPressed = _inputs.Keyboard.AnyPressed;

        if (allowRepeating)
        {
            foreach (var (keyCode, key) in _repeatableKeys)
            {
                isPressed |= key.WasRepeated;
            }
        }

        return isPressed;
    }

    public bool IsAnyKeyDown(ReadOnlySpan<KeyCode> keyCodes)
    {
        return KeyboardEnabled && _inputs.Keyboard.IsAnyKeyDown(keyCodes);
    }

    public bool IsKeyDown(KeyCode key)
    {
        return KeyboardEnabled && _inputs.Keyboard.IsDown(key);
    }

    public bool IsKeyReleased(KeyCode key)
    {
        return KeyboardEnabled && _inputs.Keyboard.IsReleased(key);
    }

    public bool IsMouseButtonDown(MouseButtonCode mouseButton)
    {
        if (!MouseEnabled)
            return false;

        return mouseButton switch
        {
            MouseButtonCode.Left => _inputs.Mouse.LeftButton.IsDown,
            MouseButtonCode.Right => _inputs.Mouse.RightButton.IsDown,
            MouseButtonCode.Middle => _inputs.Mouse.MiddleButton.IsDown,
            MouseButtonCode.X1 => _inputs.Mouse.X1Button.IsDown,
            MouseButtonCode.X2 => _inputs.Mouse.X2Button.IsDown,
            _ => throw new InvalidOperationException(),
        };
    }

    public bool IsMouseButtonHeld(MouseButtonCode mouseButton)
    {
        if (!MouseEnabled)
            return false;

        return mouseButton switch
        {
            MouseButtonCode.Left => _inputs.Mouse.LeftButton.IsHeld,
            MouseButtonCode.Right => _inputs.Mouse.RightButton.IsHeld,
            MouseButtonCode.Middle => _inputs.Mouse.MiddleButton.IsHeld,
            MouseButtonCode.X1 => _inputs.Mouse.X1Button.IsHeld,
            MouseButtonCode.X2 => _inputs.Mouse.X2Button.IsHeld,
            _ => throw new InvalidOperationException(),
        };
    }

    public ReadOnlySpan<char> GetTextInput()
    {
        if (!KeyboardEnabled)
            return Array.Empty<char>();
        return new ReadOnlySpan<char>(_textInput, 0, _numTextInputChars);
    }

    public bool IsMouseButtonPressed(MouseButtonCode mouseButton)
    {
        if (!MouseEnabled)
            return false;

        return mouseButton switch
        {
            MouseButtonCode.Left => _inputs.Mouse.LeftButton.IsPressed,
            MouseButtonCode.Right => _inputs.Mouse.RightButton.IsPressed,
            MouseButtonCode.Middle => _inputs.Mouse.MiddleButton.IsPressed,
            MouseButtonCode.X1 => _inputs.Mouse.X1Button.IsPressed,
            MouseButtonCode.X2 => _inputs.Mouse.X2Button.IsPressed,
            _ => throw new InvalidOperationException(),
        };
    }

    public bool IsMouseButtonReleased(MouseButtonCode mouseButton)
    {
        if (!MouseEnabled)
            return false;

        return mouseButton switch
        {
            MouseButtonCode.Left => _inputs.Mouse.LeftButton.IsReleased,
            MouseButtonCode.Right => _inputs.Mouse.RightButton.IsReleased,
            MouseButtonCode.Middle => _inputs.Mouse.MiddleButton.IsReleased,
            MouseButtonCode.X1 => _inputs.Mouse.X1Button.IsReleased,
            MouseButtonCode.X2 => _inputs.Mouse.X2Button.IsReleased,
            _ => throw new InvalidOperationException(),
        };
    }

    public void SetViewportTransform(Matrix4x4 viewportTransform)
    {
        Matrix4x4.Invert(ref viewportTransform, out _viewportInvert);
    }
}
