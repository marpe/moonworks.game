using System.Diagnostics.CodeAnalysis;
using MyGame.Cameras;

namespace MyGame.Input;

public static class Binds
{
    public const int MouseWheelUp = 0;
    public const int MouseWheelDown = 1;

    private static readonly Dictionary<string, string> _binds = new(StringComparer.InvariantCultureIgnoreCase);

    public static readonly Dictionary<MouseButtonCode, string> MouseButtonCodeToName;
    public static readonly Dictionary<int, string> MouseWheelCodeToName;
    public static readonly Dictionary<KeyCode, string> KeyCodeToName;

    private static readonly Dictionary<string, KeyCode> _keyCodeNames = new(StringComparer.InvariantCultureIgnoreCase)
    {
        { "a", KeyCode.A },
        { "b", KeyCode.B },
        { "c", KeyCode.C },
        { "d", KeyCode.D },
        { "e", KeyCode.E },
        { "f", KeyCode.F },
        { "g", KeyCode.G },
        { "h", KeyCode.H },
        { "i", KeyCode.I },
        { "j", KeyCode.J },
        { "k", KeyCode.K },
        { "l", KeyCode.L },
        { "m", KeyCode.M },
        { "n", KeyCode.N },
        { "o", KeyCode.O },
        { "p", KeyCode.P },
        { "q", KeyCode.Q },
        { "r", KeyCode.R },
        { "s", KeyCode.S },
        { "t", KeyCode.T },
        { "u", KeyCode.U },
        { "v", KeyCode.V },
        { "w", KeyCode.W },
        { "x", KeyCode.X },
        { "y", KeyCode.Y },
        { "z", KeyCode.Z },
        { "1", KeyCode.D1 },
        { "2", KeyCode.D2 },
        { "3", KeyCode.D3 },
        { "4", KeyCode.D4 },
        { "5", KeyCode.D5 },
        { "6", KeyCode.D6 },
        { "7", KeyCode.D7 },
        { "8", KeyCode.D8 },
        { "9", KeyCode.D9 },
        { "0", KeyCode.D0 },
        { "return", KeyCode.Return },
        { "escape", KeyCode.Escape },
        { "backspace", KeyCode.Backspace },
        { "tab", KeyCode.Tab },
        { "space", KeyCode.Space },
        { "-", KeyCode.Minus },
        { "=", KeyCode.Equals },
        { "[", KeyCode.LeftBracket },
        { "]", KeyCode.RightBracket },
        { "\\", KeyCode.Backslash },
        { ";", KeyCode.Semicolon },
        { "'", KeyCode.Apostrophe },
        { "`", KeyCode.Grave },
        { ",", KeyCode.Comma },
        { ".", KeyCode.Period },
        { "/", KeyCode.Slash },
        { "capslock", KeyCode.CapsLock },
        { "f1", KeyCode.F1 },
        { "f2", KeyCode.F2 },
        { "f3", KeyCode.F3 },
        { "f4", KeyCode.F4 },
        { "f5", KeyCode.F5 },
        { "f6", KeyCode.F6 },
        { "f7", KeyCode.F7 },
        { "f8", KeyCode.F8 },
        { "f9", KeyCode.F9 },
        { "f10", KeyCode.F10 },
        { "f11", KeyCode.F11 },
        { "f12", KeyCode.F12 },
        { "print_screen", KeyCode.PrintScreen },
        { "scroll_lock", KeyCode.ScrollLock },
        { "pause", KeyCode.Pause },
        { "insert", KeyCode.Insert },
        { "home", KeyCode.Home },
        { "page_up", KeyCode.PageUp },
        { "delete", KeyCode.Delete },
        { "end", KeyCode.End },
        { "page_down", KeyCode.PageDown },
        { "right", KeyCode.Right },
        { "left", KeyCode.Left },
        { "down", KeyCode.Down },
        { "up", KeyCode.Up },
        { "kp_divide", KeyCode.KeypadDivide },
        { "kp_multiply", KeyCode.KeypadMultiply },
        { "kp_minus", KeyCode.KeypadMinus },
        { "kp_plus", KeyCode.KeypadPlus },
        { "kp_enter", KeyCode.KeypadEnter },
        { "kp_1", KeyCode.Keypad1 },
        { "kp_2", KeyCode.Keypad2 },
        { "kp_3", KeyCode.Keypad3 },
        { "kp_4", KeyCode.Keypad4 },
        { "kp_5", KeyCode.Keypad5 },
        { "kp_6", KeyCode.Keypad6 },
        { "kp_7", KeyCode.Keypad7 },
        { "kp_8", KeyCode.Keypad8 },
        { "kp_9", KeyCode.Keypad9 },
        { "kp_0", KeyCode.Keypad0 },
        { "kp_period", KeyCode.KeypadPeriod },
        { "left_control", KeyCode.LeftControl },
        { "left_shift", KeyCode.LeftShift },
        { "left_alt", KeyCode.LeftAlt },
        { "left_meta", KeyCode.LeftMeta },
        { "right_control", KeyCode.RightControl },
        { "right_shift", KeyCode.RightShift },
        { "right_alt", KeyCode.RightAlt },
        { "right_meta", KeyCode.RightMeta },
    };

    private static readonly Dictionary<string, MouseButtonCode> _mouseButtonNames = new(StringComparer.InvariantCultureIgnoreCase)
    {
        { "mb_left", MouseButtonCode.Left },
        { "mb_right", MouseButtonCode.Right },
        { "mb_middle", MouseButtonCode.Middle },
        { "mb_x1", MouseButtonCode.X1 },
        { "mb_x2", MouseButtonCode.X2 },
    };

    private static readonly Dictionary<string, int> _mouseWheelNames = new(StringComparer.InvariantCultureIgnoreCase)
    {
        { "mwheelup", MouseWheelUp },
        { "mwheeldown", MouseWheelDown },
    };

    static Binds()
    {
        MouseButtonCodeToName = _mouseButtonNames.ToDictionary(x => x.Value, x => x.Key);
        MouseWheelCodeToName = _mouseWheelNames.ToDictionary(x => x.Value, x => x.Key);
        KeyCodeToName = _keyCodeNames.ToDictionary(x => x.Value, x => x.Key);
    }

    public static void Initialize()
    {
        var defaultBinds = new[]
        {
            ("f1", "imgui.hidden"),
            ("f2", "noclip"),
            ("f4", "world.debug"),
            ("f5", "kill_all"),
            ("f9", "step"),
            ("f8", "restart"),
            ("f10", "pause"),
            ("f12", "screenshot"),
            ("page_up", "speed_up"),
            ("page_down", "speed_down"),
            ("p", "pause"),
            ("mb_left", "+fire1"),
            ("1", "save_pos"),
            ("2", "load_pos"),
            ("left", "+left"),
            ("right", "+right"),
            ("up", "+jump"),
            (",", "prev_level"),
            (".", "next_level"),
            ("3", "pause"),
            ("4", "step"),
            ("mb_middle", "+cam_pan"),
        };

        var keyBinds = new[]
        {
            ("right", "Move right", KeyCode.D, PlayerBinds.Right),
            ("left", "Move left", KeyCode.A, PlayerBinds.Left),
            ("jump", "Jump", KeyCode.Space, PlayerBinds.Jump),
            ("fire1", "Fire", KeyCode.LeftControl, PlayerBinds.Fire1),
            ("respawn", "Respawn", KeyCode.Insert, PlayerBinds.Respawn),
            ("mousemove", "Move to mouse", KeyCode.M, PlayerBinds.MoveToMouse),

            ("cam_zoom_in", "Increase camera zoom", KeyCode.D0, CameraBinds.ZoomIn),
            ("cam_zoom_out", "Decrease camera zoom", KeyCode.Minus, CameraBinds.ZoomOut),
            ("cam_up", "Move camera up", KeyCode.U, CameraBinds.Up),
            ("cam_down", "Move camera down", KeyCode.O, CameraBinds.Down),
            ("cam_forward", "Move camera forward", KeyCode.I, CameraBinds.Forward),
            ("cam_back", "Move camera back", KeyCode.K, CameraBinds.Back),
            ("cam_left", "Move camera left", KeyCode.J, CameraBinds.Left),
            ("cam_right", "Move camera right", KeyCode.L, CameraBinds.Right),
            ("cam_reset", "Reset camera", KeyCode.Home, CameraBinds.Reset),
        };

        var mouseBinds = new[]
        {
            ("cam_pan", "Pan camera", MouseButtonCode.Right, CameraBinds.Pan),
        };

        for (var i = 0; i < defaultBinds.Length; i++)
        {
            var (key, cmd) = defaultBinds[i];
            Bind(key, cmd);
        }

        for (var i = 0; i < keyBinds.Length; i++)
        {
            var (cmd, description, defaultBind, bind) = keyBinds[i];
            Bind(KeyCodeToName[defaultBind], $"+{cmd}");
            RegisterConsoleCommandForBind(cmd, description, bind);
        }

        for (var i = 0; i < mouseBinds.Length; i++)
        {
            var (cmd, description, button, bind) = mouseBinds[i];
            Bind(MouseButtonCodeToName[button], $"+{cmd}");
            RegisterConsoleCommandForBind(cmd, description, bind);
        }

        TWConsole.TWConsole.OnCfgSave += builder =>
        {
            builder.AppendLine("unbindall");
            var sb = GetBindsAsText();
            builder.Append(sb);
        };
    }

    private static void RegisterConsoleCommandForBind(ReadOnlySpan<char> cmdName, ReadOnlySpan<char> description, ButtonBind bind)
    {
        ConsoleCommand.ConsoleCommandHandler downHandler = (console, cmd, args) =>
        {
            var wasActive = bind.Active;
            bind.Active = true;
            bind.WasActive = wasActive;
            bind.Sources[0] = args.Length > 1 ? args[1] : "";
            bind.Frame = Shared.Game.Time.UpdateCount;
            bind.Timestamp = Shared.Game.Time.TotalElapsedTime;
        };
        ConsoleCommand.ConsoleCommandHandler upHandler = (console, cmd, args) =>
        {
            bind.Active = false;
            bind.WasActive = false;
            bind.Sources[0] = args.Length > 1 ? args[1] : "";
            bind.Frame = Shared.Game.Time.UpdateCount;
            bind.Timestamp = Shared.Game.Time.TotalElapsedTime;
        };
        var downCmd = new ConsoleCommand($"+{cmdName}", description.ToString(), downHandler, Array.Empty<ConsoleCommandArg>(), Array.Empty<string>(), false);
        var upCmd = new ConsoleCommand($"-{cmdName}", description.ToString(), upHandler, Array.Empty<ConsoleCommandArg>(), Array.Empty<string>(), false);
        Shared.Console.RegisterCommand(downCmd);
        Shared.Console.RegisterCommand(upCmd);
    }

    public static bool TryGetBind(string key, [NotNullWhen(true)] out string? bind)
    {
        return _binds.TryGetValue(key, out bind);
    }

    [ConsoleHandler("unbindall", "Removes all binds")]
    public static void UnbindAll()
    {
        _binds.Clear();
    }

    [ConsoleHandler("list_keys", "List key codes")]
    private static void PrintKeyNames()
    {
        var keyNames = Enum.GetNames<KeyCode>();
        Shared.Console.Print($"Available key names are:\n{string.Join('\n', keyNames)}");
    }

    [ConsoleHandler("list_binds", "List all binds")]
    private static void PrintBinds()
    {
        var sb = GetBindsAsText();
        Shared.Console.Print(sb.ToString());
    }

    [ConsoleHandler("bind", "Bind an action to a key")]
    public static void Bind(string keyStr, string cmdStr = "")
    {
        if (string.IsNullOrWhiteSpace(cmdStr))
        {
            if (_binds.ContainsKey(keyStr))
                Shared.Console.Print($"bind {keyStr} {_binds[keyStr]}");
            else
                Shared.Console.Print($"Button {keyStr} is unbound");
            return;
        }

        var isValidKeyStr = _keyCodeNames.ContainsKey(keyStr) ||
                            _mouseButtonNames.ContainsKey(keyStr) ||
                            _mouseWheelNames.ContainsKey(keyStr);

        if (!isValidKeyStr)
        {
            Shared.Console.Print($"\"{keyStr}\" is not a valid key");
        }

        _binds[keyStr] = cmdStr;
    }

    [ConsoleHandler("unbind", "Unbind a key")]
    public static void Unbind(string keyStr)
    {
        if (_binds.Keys.Contains(keyStr))
            _binds.Remove(keyStr);
        else
            Shared.Console.Print($"Button {keyStr} is unbound");
    }

    public static StringBuilder GetBindsAsText()
    {
        var sb = new StringBuilder();

        foreach (var (key, cmd) in _binds)
        {
            sb.AppendLine($"bind {key.ToLowerInvariant(),-20} \"{cmd}\"");
        }

        return sb;
    }
}
