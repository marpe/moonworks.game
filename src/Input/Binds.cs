using System.Diagnostics.CodeAnalysis;

namespace MyGame.Input;

public static class BindHandler
{
    private static HashSet<int> _buttonsToClear = new();

    private static Dictionary<int, Binds.ButtonBind> _buttons = new();
    private static readonly Func<int, string> _getMouseButtonName;
    private static readonly Func<int, string> _getMouseWheelName;
    private static readonly Func<int, string> _getKeyboardButtonName;
    public static IReadOnlyDictionary<int, Binds.ButtonBind> Buttons => _buttons;

    private const int MOUSE_WHEEL_OFFSET = 200;
    private const int MOUSE_BUTTON_OFFSET = 100;

    static BindHandler()
    {
        _getMouseButtonName = buttonId => Binds.MouseButtonCodeToName[(MouseButtonCode)(buttonId - MOUSE_BUTTON_OFFSET)];
        _getMouseWheelName = buttonId => Binds.MouseWheelCodeToName[buttonId - MOUSE_WHEEL_OFFSET];
        _getKeyboardButtonName = buttonId => Binds.KeyCodeToName[(KeyCode)buttonId];
    }

    private static bool IsButtonDown(int buttonId)
    {
        return _buttons.ContainsKey(buttonId) && _buttons[buttonId].Active;
    }

    private static bool IsButtonReleased(int buttonId)
    {
        return _buttons.ContainsKey(buttonId) && _buttons[buttonId].WasActive && !_buttons[buttonId].Active;
    }

    private static bool IsButtonPressed(int buttonId)
    {
        return _buttons.ContainsKey(buttonId) && !_buttons[buttonId].WasActive && _buttons[buttonId].Active;
    }

    public static void AddState(in InputState inputState)
    {
        foreach (var key in inputState.KeysDown)
        {
            var buttonId = (int)key;
            if (!_buttons.ContainsKey(buttonId))
                _buttons[buttonId] = new Binds.ButtonBind();

            var state = _buttons[buttonId];
            state.WasActive = state.Active;
            state.Active = true;
            state.GameUpdateCount = Shared.Game.Time.UpdateCount;
        }

        foreach (var mouseButton in inputState.MouseButtonsDown)
        {
            var buttonId = ((int)mouseButton) + 100;
            if (!_buttons.ContainsKey(buttonId))
                _buttons[buttonId] = new Binds.ButtonBind();

            var state = _buttons[buttonId];
            state.WasActive = state.Active;
            state.Active = true;
            state.GameUpdateCount = Shared.Game.Time.UpdateCount;
        }

        _buttonsToClear.Clear();
        foreach (var (id, button) in _buttons)
        {
            // if it was updated this frame, continue
            if (button.GameUpdateCount == Shared.Game.Time.UpdateCount)
                continue;
            button.WasActive = button.Active;
            button.Active = false;

            if (!button.WasActive && !button.Active)
                _buttonsToClear.Add(id);
        }

        foreach (var id in _buttonsToClear)
        {
            _buttons.Remove(id);
        }

        var mwheelUpId = Binds.MouseWheelUp + MOUSE_WHEEL_OFFSET;
        var mwheelDownId = Binds.MouseWheelDown + MOUSE_WHEEL_OFFSET;
        if (!_buttons.ContainsKey(mwheelUpId))
            _buttons[mwheelUpId] = new Binds.ButtonBind();
        _buttons[mwheelUpId].WasActive = _buttons[mwheelUpId].Active;
        _buttons[mwheelUpId].Active = inputState.MouseWheelDelta > 0;
        _buttons[mwheelUpId].GameUpdateCount = Shared.Game.Time.UpdateCount;
        if (!_buttons.ContainsKey(mwheelDownId))
            _buttons[mwheelDownId] = new Binds.ButtonBind();
        _buttons[mwheelDownId].WasActive = _buttons[mwheelDownId].Active;
        _buttons[mwheelDownId].Active = inputState.MouseWheelDelta < 0;
        _buttons[mwheelDownId].GameUpdateCount = Shared.Game.Time.UpdateCount;
    }

    public static void Clear()
    {
        _buttons.Clear();
    }

    public static void HandleBoundKeys()
    {
        foreach (var (code, _) in Binds.MouseWheelCodeToName)
        {
            HandleButtonBind(code + MOUSE_WHEEL_OFFSET, _getMouseWheelName);
        }

        foreach (var (code, _) in Binds.MouseButtonCodeToName)
        {
            HandleButtonBind(((int)code + MOUSE_BUTTON_OFFSET), _getMouseButtonName);
        }

        foreach (var (code, _) in Binds.KeyCodeToName)
        {
            // console key handled separately
            if (code == KeyCode.Grave && IsButtonPressed((int)code))
            {
                ConsoleScreen.ToggleConsole();
                continue;
            }

            HandleButtonBind((int)code, _getKeyboardButtonName);
        }
    }

    private static void HandleButtonBind(int buttonId, Func<int, string> getButtonName)
    {
        var keyStr = getButtonName(buttonId);

        if (!Binds.TryGetBind(keyStr, out var bindStr))
            return;

        var split = ConsoleUtils.SplitArgs(bindStr);
        var cmdKey = split[0];
        if (!Shared.Console.Commands.TryGetValue(cmdKey, out var cmd))
        {
            Logs.LogError($"Command not found: {keyStr} -> {cmdKey}");
            return;
        }

        if (IsButtonReleased(buttonId))
        {
            // key up events only trigger binds if the binding is a button command (leading + sign)
            if (!cmdKey.StartsWith('+'))
                return;

            var upCmdKey = $"-{cmdKey.AsSpan().Slice(1)}";
            var upCmd = Shared.Console.Commands[upCmdKey];
            Shared.Console.ExecuteCommand(upCmd, new[] { cmdKey, keyStr });
            return;
        }

        if (IsButtonPressed(buttonId))
        {
            // ignore if console is down
            if (!Shared.Game.ConsoleScreen.IsHidden)
                return;

            // handle toggling of bool cvars
            if (split.Length == 1 && cmd.IsCVar && Shared.Console.CVars.TryGetValue(cmdKey, out var cvar) && cvar.VarType == typeof(bool))
            {
                var curr = cvar.GetValue<bool>();
                Shared.Console.ExecuteCommand(cmd, new[] { cmdKey, (!curr).ToString() });
                return;
            }

            if (cmdKey.StartsWith('+'))
            {
                Shared.Console.ExecuteCommand(cmd, new[] { cmdKey, keyStr });
                return;
            }

            Shared.Console.ExecuteCommand(cmd, split);
            return;
        }

        if (IsButtonDown(buttonId))
        {
            // continued press
            if (!cmdKey.StartsWith('+'))
                return;

            Shared.Console.ExecuteCommand(cmd, new[] { cmdKey, keyStr });

            return;
        }

        if (!IsButtonDown(buttonId))
        {
            // released/idle key
            if (!cmdKey.StartsWith('+'))
                return;

            var upCmdKey = $"-{cmdKey.AsSpan().Slice(1)}";
            var upCmd = Shared.Console.Commands[upCmdKey];
            Shared.Console.ExecuteCommand(upCmd, new[] { cmdKey, keyStr });
            return;
        }
    }
}

public static class Binds
{
    public class ButtonBind
    {
        public string[] Sources = { "", "" }; // The id of the button which triggered this bind
        public float GameTimestamp;
        public float WorldTimestamp;
        public float TimeHeld;
        public bool Active;
        public bool WasActive;
        public ulong GameUpdateCount;
        public ulong WorldUpdateCount;
    }

    public static class Camera
    {
        public static ButtonBind ZoomIn = new();
        public static ButtonBind ZoomOut = new();
        public static ButtonBind Up = new();
        public static ButtonBind Down = new();
        public static ButtonBind Forward = new();
        public static ButtonBind Back = new();
        public static ButtonBind Right = new();
        public static ButtonBind Left = new();
        public static ButtonBind Pan = new();
        public static ButtonBind Reset = new();
    }

    public static class Player
    {
        public static ButtonBind Right = new();
        public static ButtonBind Left = new();
        public static ButtonBind Jump = new();
        public static ButtonBind Fire1 = new();
        public static ButtonBind Respawn = new();
        public static ButtonBind MoveToMouse = new();

        public static PlayerCommand ToPlayerCommand()
        {
            var cmd = new PlayerCommand();

            if (Right.Active)
                cmd.MovementX += 1;

            if (Left.Active)
                cmd.MovementX += -1;

            cmd.IsFiring = !Fire1.WasActive && Fire1.Active;
            cmd.Respawn = !Respawn.WasActive && Respawn.Active;
            cmd.IsJumpDown = Jump.Active;
            cmd.IsJumpPressed = !Jump.WasActive && Jump.Active;
            cmd.MoveToMouse = MoveToMouse.Active;

            return cmd;
        }
    }

    public const int MouseWheelUp = 0;
    public const int MouseWheelDown = 1;

    private static readonly Dictionary<string, string> _binds = new(StringComparer.InvariantCultureIgnoreCase);

    #region Key/Button Names

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

    #endregion

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
            ("right", "Move right", KeyCode.D, Player.Right),
            ("left", "Move left", KeyCode.A, Player.Left),
            ("jump", "Jump", KeyCode.Space, Player.Jump),
            ("fire1", "Fire", KeyCode.LeftControl, Player.Fire1),
            ("respawn", "Respawn", KeyCode.Insert, Player.Respawn),
            ("mousemove", "Move to mouse", KeyCode.M, Player.MoveToMouse),

            ("cam_zoom_in", "Increase camera zoom", KeyCode.D0, Camera.ZoomIn),
            ("cam_zoom_out", "Decrease camera zoom", KeyCode.Minus, Camera.ZoomOut),
            ("cam_up", "Move camera up", KeyCode.U, Camera.Up),
            ("cam_down", "Move camera down", KeyCode.O, Camera.Down),
            ("cam_forward", "Move camera forward", KeyCode.I, Camera.Forward),
            ("cam_back", "Move camera back", KeyCode.K, Camera.Back),
            ("cam_left", "Move camera left", KeyCode.J, Camera.Left),
            ("cam_right", "Move camera right", KeyCode.L, Camera.Right),
            ("cam_reset", "Reset camera", KeyCode.Home, Camera.Reset),
        };

        var mouseBinds = new[]
        {
            ("cam_pan", "Pan camera", MouseButtonCode.Right, Camera.Pan),
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
            // don't trigger down actions if the world hasn't been updated yet
            // unless it was a command types in the console (in which case no source button is in the args)
            if (bind.WorldUpdateCount != 0 && Shared.Game.World.IsLoaded &&
                bind.WorldUpdateCount == Shared.Game.World.WorldUpdateCount &&
                args.Length > 1)
                return;
            var wasActive = bind.Active;
            bind.Active = true;
            bind.WasActive = wasActive;
            bind.Sources[0] = args.Length > 1 ? args[1] : "";
            bind.GameUpdateCount = Shared.Game.Time.UpdateCount;
            bind.GameTimestamp = Shared.Game.Time.TotalElapsedTime;
            bind.WorldUpdateCount = Shared.Game.World?.WorldUpdateCount ?? 0;
            bind.WorldTimestamp = Shared.Game.World?.WorldTotalElapsedTime ?? 0;
        };
        ConsoleCommand.ConsoleCommandHandler upHandler = (console, cmd, args) =>
        {
            // don't trigger up actions if the world hasn't been updated yet
            if (bind.WorldUpdateCount != 0 && Shared.Game.World.IsLoaded &&
                bind.WorldUpdateCount == Shared.Game.World.WorldUpdateCount)
                return;
            // also skip if sources is empty, since it means the bind was triggered by typing it in the console
            // in which case we want to repeat the action, unless the -command was typed in which case args wont contain a button source
            if (bind.Sources[0] == "" && args.Length > 1)
                return;
            var wasActive = bind.Active;
            bind.Active = false;
            bind.WasActive = wasActive;
            bind.Sources[0] = args.Length > 1 ? args[1] : "";
            bind.GameUpdateCount = Shared.Game.Time.UpdateCount;
            bind.GameTimestamp = Shared.Game.Time.TotalElapsedTime;
            bind.WorldUpdateCount = Shared.Game.World?.WorldUpdateCount ?? 0;
            bind.WorldTimestamp = Shared.Game.World?.WorldTotalElapsedTime ?? 0;
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
