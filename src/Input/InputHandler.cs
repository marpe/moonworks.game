namespace MyGame.Input;

public class ButtonBind
{
    public int[] Sources = { 0, 0 };
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

    public static KeyCode[] KeyCodes = Enum.GetValues<KeyCode>();
    public static Dictionary<KeyCode, string> KeyStrings = new();

    public static MouseButtonCode[] MouseButtonsCodes = Enum.GetValues<MouseButtonCode>();

    static InputHandler()
    {
        foreach (var keyCode in KeyCodes)
            KeyStrings.Add(keyCode, keyCode.ToString());
    }

    private readonly Inputs _inputs;

    private readonly Dictionary<KeyCode, RepeatableKey> _repeatableKeys = new();

    private char[] _textInput = new char[128];
    private int _numTextInputChars = 0;

    public bool KeyboardEnabled = true;
    public bool MouseEnabled = true;
    private Matrix4x4 _viewportInvert = Matrix4x4.Identity;

    private Dictionary<string, ButtonBind> _inputBinds = new();

    public InputHandler(Inputs inputs)
    {
        _inputs = inputs;
        Inputs.TextInput += OnTextInput;

        var inputCommands = new[]
        {
            ("right", "Move right", KeyCode.D, PlayerBinds.Right),
            ("left", "Move left", KeyCode.A, PlayerBinds.Left),
            ("jump", "Jump", KeyCode.Space, PlayerBinds.Jump),
            ("fire1", "Fire", KeyCode.LeftControl, PlayerBinds.Fire1),
            ("respawn", "Respawn", KeyCode.Insert, PlayerBinds.Respawn)
        };

        for (var i = 0; i < inputCommands.Length; i++)
        {
            var (cmd, description, defaultBind, bind) = inputCommands[i];

            Binds.Bind(defaultBind.ToString(), $"+{cmd}");

            var args = new ConsoleCommandArg[] { new("Source", true, -1, typeof(int)) };
            ConsoleCommand.ConsoleCommandHandler downHandler = (console, cmd, args) =>
            {
                var wasActive = bind.Active;
                bind.Active = true;
                bind.WasActive = wasActive;
                bind.Sources[0] = args.Length > 1 ? (int)Enum.Parse<KeyCode>(args[1]) : -1;
                bind.Frame = Shared.Game.Time.UpdateCount;
                bind.Timestamp = Shared.Game.Time.TotalElapsedTime;
            };
            ConsoleCommand.ConsoleCommandHandler upHandler = (console, cmd, args) =>
            {
                bind.Active = false;
                bind.WasActive = false;
                bind.Sources[0] = args.Length > 1 ? (int)Enum.Parse<KeyCode>(args[1]) : -1;
                bind.Frame = Shared.Game.Time.UpdateCount;
                bind.Timestamp = Shared.Game.Time.TotalElapsedTime;
            };
            var downCmd = new ConsoleCommand($"+{cmd}", description, downHandler, args, Array.Empty<string>(), false);
            var upCmd = new ConsoleCommand($"-{cmd}", description, upHandler, args, Array.Empty<string>(), false);
            Shared.Console.RegisterCommand(downCmd);
            Shared.Console.RegisterCommand(upCmd);
        }
    }

    public Point MouseDelta => MouseEnabled ? new(_inputs.Mouse.DeltaX, _inputs.Mouse.DeltaY) : Point.Zero;

    public Point MousePosition
    {
        get
        {
            if (!MouseEnabled)
                return Point.Zero;

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

    // TODO (marpe): Cleanup and optimize
    private void HandleBoundKeys()
    {
        for (var i = 0; i < KeyCodes.Length; i++)
        {
            var keyCode = KeyCodes[i];

            // console key handled separately
            if (keyCode == KeyCode.Grave && IsKeyPressed(keyCode))
            {
                ConsoleScreen.ToggleConsole();
                continue;
            }

            var keyStr = KeyStrings[keyCode];
            if (!IsKeyDown(keyCode))
            {
                if (!IsKeyReleased(keyCode))
                    continue;

                if (!Binds.TryGetBind(keyStr, out var bind))
                    continue;

                var split = ConsoleUtils.SplitArgs(bind);
                var cmdKey = split[0];

                if (!cmdKey.StartsWith('+'))
                    continue;

                var upCmdKey = $"-{cmdKey.AsSpan().Slice(1)}";
                if (Shared.Console.Commands.ContainsKey(upCmdKey))
                {
                    Shared.Console.ExecuteCommand(Shared.Console.Commands[upCmdKey], new[] { cmdKey, keyCode.ToString() });
                }
                else
                {
                    Logger.LogError($"Command not found: {keyStr} -> {cmdKey}");
                }
            }
            else
            {
                if (!IsKeyPressed(keyCode))
                    continue;

                // ignore if console is down
                if (!Shared.Game.ConsoleScreen.IsHidden)
                    continue;

                if (!Binds.TryGetBind(keyStr, out var bind))
                    continue;

                var split = ConsoleUtils.SplitArgs(bind);
                var cmdKey = split[0];

                if (Shared.Console.CVars.ContainsKey(cmdKey))
                {
                    var cvar = Shared.Console.CVars[cmdKey];
                    if (cvar.VarType == typeof(bool))
                    {
                        var curr = cvar.GetValue<bool>();
                        Shared.Console.ExecuteCommand(Shared.Console.Commands[cmdKey], new[] { cmdKey, (!curr).ToString() });
                        continue;
                    }
                }

                if (Shared.Console.Commands.ContainsKey(cmdKey))
                {
                    if (cmdKey.StartsWith('+') || cmdKey.StartsWith('-'))
                    {
                        Shared.Console.ExecuteCommand(Shared.Console.Commands[cmdKey], new[] { cmdKey, keyCode.ToString() });
                    }
                    else
                    {
                        Shared.Console.ExecuteCommand(Shared.Console.Commands[cmdKey], split);
                    }
                }
                else
                {
                    Logger.LogError($"Command not found: {keyStr} -> {cmdKey}");
                }
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

    public void SetViewportTransform(Matrix4x4 viewportTransform)
    {
        Matrix4x4.Invert(ref viewportTransform, out _viewportInvert);
    }
}
