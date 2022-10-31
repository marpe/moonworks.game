using MyGame.Utils;

namespace MyGame;

public class RepeatableKey
{
    public float RepeatTimer;
    public bool WasRepeated;

    public void Update(bool isHeld, float deltaSeconds)
    {
        WasRepeated = false;
        RepeatTimer = isHeld ? RepeatTimer + deltaSeconds : 0;
        if (RepeatTimer >= InputHandler.INITIAL_REPEAT_DELAY + InputHandler.REPEAT_DELAY)
        {
            WasRepeated = true;
            RepeatTimer -= InputHandler.REPEAT_DELAY;
        }
    }
}

public struct InputState
{
    public static readonly Vector2 DefaultMousePosition = new(-float.MaxValue, -float.MaxValue);
    public bool[] KeyboardState = new bool[232];
    public bool[] MouseState = new bool[3];
    public Vector2 GlobalMousePosition = DefaultMousePosition;
    public int MouseWheelDelta = 0;
    public char[] TextInput = Array.Empty<char>();
    public int NumTextInputChars = 0;

    public InputState()
    {
    }

    public static void Clear(ref InputState inputState)
    {
        inputState.KeyboardState.AsSpan().Fill(false);
        inputState.MouseState.AsSpan().Fill(false);
        inputState.GlobalMousePosition = DefaultMousePosition;
        inputState.MouseWheelDelta = 0;
        inputState.TextInput.AsSpan().Fill('\0');
        inputState.NumTextInputChars = 0;
    }
    
    public static bool IsKeyDown(in InputState inpuState, KeyCode keyCode)
    {
        return inpuState.KeyboardState[(int)keyCode];
    }

    public static bool IsAnyKeyDown(in InputState inputState, KeyCode[] keyCodes)
    {
        for (var i = 0; i < keyCodes.Length; i++)
        {
            if (inputState.KeyboardState[(int)keyCodes[i]])
                return true;
        }

        return false;
    }

    public static bool IsMouseButtonDown(in InputState inputState, MouseButtonCode mouseButton)
    {
        return inputState.MouseState[(int)mouseButton];
    }
}

public class InputHandler
{
    /// <summary>
    /// Number of seconds between each registered keypress when a key is being held down
    /// </summary>
    public const float REPEAT_DELAY = 0.03f;

    /// <summary>
    /// Number of seconds a key can be held down before being repeated
    /// </summary>
    public const float INITIAL_REPEAT_DELAY = 0.5f;

    private readonly MyGameMain _game;

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

    public Point MouseDelta => new(_inputs.Mouse.DeltaX, _inputs.Mouse.DeltaY);

    public int MouseWheelDelta => _inputs.Mouse.Wheel;

    public List<char> TextInput = new();

    private Dictionary<KeyCode, RepeatableKey> _repeatableKeys = new();

    public InputHandler(MyGameMain game)
    {
        _game = game;
        _inputs = game.Inputs;
        Inputs.TextInput += OnTextInput;
    }

    /// <summary>
    /// https://github.com/FNA-XNA/FNA/wiki/5:-FNA-Extensions#textinputext
    /// </summary>
    /// <param name="c"></param>
    private void OnTextInput(char c)
    {
        TextInput.Add(c);
    }

    public void BeginFrame()
    {
        foreach (var (keyCode, key) in _repeatableKeys)
        {
            var isHeld = _inputs.Keyboard.IsHeld(keyCode);
            key.Update(isHeld, _game.ElapsedTime);
        }
    }

    public void EndFrame()
    {
        TextInput.Clear();
    }

    public bool IsAnyModifierKeyDown()
    {
        return _inputs.Keyboard.IsAnyKeyDown(ModifierKeys);
    }

    public bool IsKeyPressed(KeyCode key, bool allowRepeating = false)
    {
        var isPressed = _inputs.Keyboard.IsPressed(key);

        if (allowRepeating)
        {
            if (!_repeatableKeys.ContainsKey(key))
                _repeatableKeys.Add(key, new RepeatableKey());
            isPressed |= _repeatableKeys[key].WasRepeated;
        }

        return isPressed;
    }

    public bool IsAnyKeyPressed(bool allowRepeating = false)
    {
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
        return _inputs.Keyboard.IsAnyKeyDown(keyCodes);
    }

    public bool IsKeyDown(KeyCode key)
    {
        return _inputs.Keyboard.IsDown(key);
    }

    public bool IsMouseButtonDown(MouseButtonCode mouseButton)
    {
        return mouseButton switch
        {
            MouseButtonCode.Left => _inputs.Mouse.LeftButton.IsDown,
            MouseButtonCode.Right => _inputs.Mouse.RightButton.IsDown,
            MouseButtonCode.Middle => _inputs.Mouse.MiddleButton.IsDown,
            _ => throw new InvalidOperationException()
        };
    }

    public bool IsMouseButtonHeld(MouseButtonCode mouseButton)
    {
        return mouseButton switch
        {
            MouseButtonCode.Left => _inputs.Mouse.LeftButton.IsHeld,
            MouseButtonCode.Right => _inputs.Mouse.RightButton.IsHeld,
            MouseButtonCode.Middle => _inputs.Mouse.MiddleButton.IsHeld,
            _ => throw new InvalidOperationException()
        };
    }
}
