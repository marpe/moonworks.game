using MyGame.Utils;

namespace MyGame;

public struct RepeatableKey
{
    public float RepeatTimer;
    public bool WasRepeated;
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
    public const float INITIAL_REPEAT_DELAY = 0.2f;

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

    public bool IsAnyKeyPressed => _inputs.Keyboard.AnyPressed;

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
            var tmpKey = key;
            tmpKey.WasRepeated = false;
            tmpKey.RepeatTimer = isHeld ? tmpKey.RepeatTimer + _game.ElapsedTime : 0;
            if (tmpKey.RepeatTimer >= INITIAL_REPEAT_DELAY + REPEAT_DELAY)
            {
                tmpKey.WasRepeated = true;
                tmpKey.RepeatTimer -= REPEAT_DELAY;
            }

            _repeatableKeys[keyCode] = tmpKey;
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

    public bool IsKeyDown(KeyCode key)
    {
        return _inputs.Keyboard.IsDown(key);
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

    public bool IsAnyKeyDown(ReadOnlySpan<KeyCode> keyCodes)
    {
        return _inputs.Keyboard.IsAnyKeyDown(keyCodes);
    }
}
