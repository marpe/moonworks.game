using MyGame.Utils;

namespace MyGame.Input;

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
    private int _numTextInputChars = 0;

    public bool KeyboardEnabled = true;
    public bool MouseEnabled = true;

    public InputHandler(Inputs inputs)
    {
        _inputs = inputs;
        Inputs.TextInput += OnTextInput;
    }

    public Point MouseDelta => MouseEnabled ? new(_inputs.Mouse.DeltaX, _inputs.Mouse.DeltaY) : Point.Zero;
    public Point MousePosition => MouseEnabled ? new(_inputs.Mouse.X, _inputs.Mouse.Y) : Point.Zero; // TODO (marpe): Maybe use a better default?

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
        for (var i = 0; i < 232; i++)
        {
            var keyCode = (KeyCode)i;
            if (!Enum.IsDefined(keyCode) || !_inputs.Keyboard.IsPressed(keyCode))
                continue;

            // escape separately
            if (keyCode == KeyCode.Escape)
                continue;

            // console key handled separately
            if (keyCode == KeyCode.Grave)
            {
                ConsoleScreen.ToggleConsole();
                continue;
            }

            if (keyCode is >= KeyCode.A and <= KeyCode.Space && !Shared.Game.ConsoleScreen.IsHidden)
            {
                // ignore most characters when console is open
                return;
            }

            var keyStr = keyCode.ToString();
            if (!Binds.TryGetBind(keyStr, out var bind))
            {
                Logger.LogWarn($"Key {keyStr} is unbound.");
                continue;
            }

            var split = ConsoleUtils.SplitArgs(bind);
            var cmdKey = split[0];

            var con = Shared.Console;
            if (con.CVars.ContainsKey(cmdKey))
            {
                var cvar = con.CVars[cmdKey];
                if (cvar.VarType == typeof(bool))
                {
                    var curr = cvar.GetValue<bool>();
                    con.ExecuteCommand(con.Commands[cmdKey], new[] { cmdKey, (!curr).ToString() });
                    continue;
                }
            }

            if (con.Commands.ContainsKey(cmdKey))
            {
                con.ExecuteCommand(con.Commands[cmdKey], split);
            }
            else
            {
                Logger.LogError($"Command not found: {keyStr} -> {cmdKey}");
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

    public bool IsMouseButtonDown(MouseButtonCode mouseButton)
    {
        if (!MouseEnabled)
            return false;

        return mouseButton switch
        {
            MouseButtonCode.Left => _inputs.Mouse.LeftButton.IsDown,
            MouseButtonCode.Right => _inputs.Mouse.RightButton.IsDown,
            MouseButtonCode.Middle => _inputs.Mouse.MiddleButton.IsDown,
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
            _ => throw new InvalidOperationException(),
        };
    }

    public ReadOnlySpan<char> GetTextInput()
    {
        if (!KeyboardEnabled)
            return Array.Empty<char>();
        return new ReadOnlySpan<char>(_textInput, 0, _numTextInputChars);
    }
}
