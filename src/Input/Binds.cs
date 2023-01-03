using System.Diagnostics.CodeAnalysis;

namespace MyGame.Input;

public static class Binds
{
    public enum MouseWheelCode
    {
        MouseWheelUp = 0,
        MouseWheelDown = 1
    }

    public enum InputAction
    {
        Jump,
        Fire1,
        Respawn,
        MoveToMouse,
        ZoomIn,
        ZoomOut,
        Up,
        Down,
        Forward,
        Back,
        Left,
        Right,
        Reset,
        Pan,
        CameraLeft,
        CameraRight
    }

    public class ActionState
    {
        public string[] Sources = { "", "" }; // The id of the button which triggered this bind
        public bool Active;
    }

    public static class Player
    {
        private static bool _fireHeld;
        private static bool _respawnHeld;
        private static bool _jumpHeld;

        public static PlayerCommand ToPlayerCommand()
        {
            var cmd = new PlayerCommand();

            if (GetAction(InputAction.Right, out var right) && right.Active)
                cmd.MovementX += 1;

            if (GetAction(InputAction.Left, out var left) && left.Active)
                cmd.MovementX += -1;

            if (GetAction(InputAction.Fire1, out var fire1) && !fire1.Active)
                _fireHeld = false;

            if (GetAction(InputAction.Jump, out var jump) && !jump.Active)
                _jumpHeld = false;

            if (GetAction(InputAction.MoveToMouse, out var respawn) && !respawn.Active)
                _respawnHeld = false;

            var isFire1Active = fire1 is { Active: true };
            cmd.IsFiring = isFire1Active && !_fireHeld;
            if (isFire1Active)
                _fireHeld = true;

            cmd.Respawn = respawn != null && respawn.Active && !_respawnHeld;
            if (respawn != null && respawn.Active)
                _respawnHeld = true;

            var isJumpActive = jump is { Active: true };
            cmd.IsJumpDown = isJumpActive;
            cmd.IsJumpPressed = isJumpActive && !_jumpHeld;
            if (isJumpActive)
                _jumpHeld = true;

            cmd.MoveToMouse = GetAction(InputAction.MoveToMouse, out var moveToMouse) && moveToMouse.Active;

            return cmd;
        }
    }

    private static readonly Dictionary<string, string> _binds = new(StringComparer.InvariantCultureIgnoreCase);

    public static Dictionary<InputAction, ActionState> _actions = new();

    private static bool _prevMWheelUp;
    private static bool _prevMWheelDown;

    #region Key/Button Names

    public static readonly Dictionary<MouseButtonCode, string> MouseButtonCodeToName;
    public static readonly Dictionary<MouseWheelCode, string> MouseWheelCodeToName;
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

    private static readonly Dictionary<string, MouseWheelCode> _mouseWheelNames = new(StringComparer.InvariantCultureIgnoreCase)
    {
        { "mwheelup", MouseWheelCode.MouseWheelUp },
        { "mwheeldown", MouseWheelCode.MouseWheelDown },
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
            ("f8", "restart_level"),
            ("f10", "pause"),
            ("f12", "screenshot"),
            ("page_up", "speed_up"),
            ("page_down", "speed_down"),
            ("home", "speed_reset"),
            ("p", "pause"),
            ("mb_left", "+fire1"),
            ("1", "save_pos"),
            ("2", "load_pos"),
            ("left", "+left"),
            ("a", "+left"),
            ("space", "+jump"),
            ("left_control", "+fire1"),
            ("insert", "+respawn"),
            ("m", "+mousemove"),
            ("0", "+cam_zoom_in"),
            ("-", "+cam_zoom_out"),
            ("u", "+cam_up"),
            ("o", "+cam_down"),
            ("i", "+cam_forward"),
            ("k", "+cam_back"),
            ("j", "+cam_left"),
            ("l", "+cam_right"),
            ("home", "+cam_reset"),
            ("d", "+right"),
            ("right", "+right"),
            ("up", "+jump"),
            (",", "prev_level"),
            (".", "next_level"),
            ("3", "pause"),
            ("4", "step"),
            ("mb_right", "+cam_pan"),
            ("mb_middle", "+cam_pan"),
            ("mwheelup", "+cam_zoom_in"),
            ("mwheeldown", "+cam_zoom_out"),
        };

        var binds = new[]
        {
            // player
            ("right", "Move right", InputAction.Right),
            ("left", "Move left", InputAction.Left),
            ("jump", "Jump", InputAction.Jump),
            ("fire1", "Fire", InputAction.Fire1),
            ("respawn", "Respawn", InputAction.Respawn),
            ("mousemove", "Move to mouse", InputAction.MoveToMouse),

            // camera
            ("cam_up", "Move camera up", InputAction.Up),
            ("cam_down", "Move camera down", InputAction.Down),
            ("cam_forward", "Move camera forward", InputAction.Forward),
            ("cam_back", "Move camera back", InputAction.Back),
            ("cam_left", "Move camera left", InputAction.CameraLeft),
            ("cam_right", "Move camera right", InputAction.CameraRight),
            ("cam_reset", "Reset camera", InputAction.Reset),
            ("cam_pan", "Pan camera", InputAction.Pan),
        };

        for (var i = 0; i < binds.Length; i++)
        {
            var (cmd, description, action) = binds[i];
            RegisterConsoleCommandForAction(cmd, description, action);
        }

        for (var i = 0; i < defaultBinds.Length; i++)
        {
            var (key, cmd) = defaultBinds[i];
            Bind(key, cmd);
        }

        TWConsole.TWConsole.OnCfgSave += builder =>
        {
            builder.AppendLine("unbindall");
            var sb = GetBindsAsText();
            builder.Append(sb);
        };
    }

    private static void RegisterConsoleCommandForAction(ReadOnlySpan<char> cmdName, ReadOnlySpan<char> description, InputAction inputAction)
    {
        // TODO (marpe): Cleanup
        var bind = new ActionState();
        _actions.Add(inputAction, bind);
        ConsoleCommand.ConsoleCommandHandler downHandler = (console, cmd, args) =>
        {
            bind.Active = true;
            bind.Sources[0] = args.Length > 1 ? args[1] : "";
        };
        ConsoleCommand.ConsoleCommandHandler upHandler = (console, cmd, args) =>
        {
            bind.Active = false;
            bind.Sources[0] = args.Length > 1 ? args[1] : "";
        };
        var downCmd = new ConsoleCommand($"+{cmdName}", description.ToString(), downHandler, Array.Empty<ConsoleCommandArg>(), Array.Empty<string>(), false);
        var upCmd = new ConsoleCommand($"-{cmdName}", description.ToString(), upHandler, Array.Empty<ConsoleCommandArg>(), Array.Empty<string>(), false);
        Shared.Console.RegisterCommand(downCmd);
        Shared.Console.RegisterCommand(upCmd);
    }

    public static void HandleButtonBinds(InputHandler input)
    {
        for (var i = 0; i < InputHandler.KeyCodes.Length; i++)
        {
            var code = InputHandler.KeyCodes[i];

            if (input.IsKeyPressed(code))
            {
                // console key handled separately
                if (code == KeyCode.Grave)
                {
                    ConsoleScreen.ToggleConsole();
                    continue;
                }

                HandleButtonBind(true, KeyCodeToName[code]);
            }
            else if (input.IsKeyReleased(code))
            {
                HandleButtonBind(false, KeyCodeToName[code]);
            }
        }

        for (var i = 0; i < InputHandler.MouseButtonCodes.Length; i++)
        {
            var code = InputHandler.MouseButtonCodes[i];
            if (input.IsMouseButtonPressed(code))
            {
                HandleButtonBind(true, MouseButtonCodeToName[code]);
            }
            else if (input.IsMouseButtonReleased(code))
            {
                HandleButtonBind(false, MouseButtonCodeToName[code]);
            }
        }

        if (input.MouseWheelDelta > 0)
        {
            HandleButtonBind(true, MouseWheelCodeToName[MouseWheelCode.MouseWheelUp]);
            HandleButtonBind(false, MouseWheelCodeToName[MouseWheelCode.MouseWheelDown]);
            _prevMWheelUp = true;
            _prevMWheelDown = false;
        }
        else if (input.MouseWheelDelta < 0)
        {
            HandleButtonBind(false, MouseWheelCodeToName[MouseWheelCode.MouseWheelUp]);
            HandleButtonBind(true, MouseWheelCodeToName[MouseWheelCode.MouseWheelDown]);
            _prevMWheelUp = false;
            _prevMWheelDown = true;
        }
        else
        {
            if (_prevMWheelUp)
                HandleButtonBind(false, MouseWheelCodeToName[MouseWheelCode.MouseWheelUp]);
            if (_prevMWheelDown)
                HandleButtonBind(false, MouseWheelCodeToName[MouseWheelCode.MouseWheelDown]);
            _prevMWheelUp = _prevMWheelDown = false;
        }

        foreach (var (action, state) in _actions)
        {
        }
    }

    /// <summary>
    /// Pulls the bind identified by "buttonIdentifier", interprets the command string and executes it 
    /// </summary>
    /// <param name="isPressed">If the button was pressed or released</param>
    /// <param name="buttonIdentifier">The buttons identifier, e.g mwheelup, kp_enter or gamepad_xyz</param>
    private static void HandleButtonBind(bool isPressed, string buttonIdentifier)
    {
        if (!TryGetBind(buttonIdentifier, out var bindStr))
            return;

        var cmdKey = bindStr;
        if (!Shared.Console.Commands.TryGetValue(cmdKey, out var cmd))
        {
            Logs.LogError($"Command not found: {buttonIdentifier} -> {cmdKey}");
            return;
        }

        // TODO (marpe): Split bindStr

        if (!isPressed)
        {
            // key up events only trigger binds if the binding is a button command (leading + sign)
            if (!cmdKey.StartsWith('+'))
                return;

            var upCmdKey = $"-{cmdKey.AsSpan().Slice(1)}";
            var upCmd = Shared.Console.Commands[upCmdKey];
            Shared.Console.ExecuteCommand(upCmd, new[] { cmdKey, buttonIdentifier });
            return;
        }

        // ignore if console is down
        if (!Shared.Game.ConsoleScreen.IsHidden)
            return;

        // handle toggling of bool cvars
        if ( /*split.Length == 1 && */cmd.IsCVar && Shared.Console.CVars.TryGetValue(cmdKey, out var cvar) && cvar.VarType == typeof(bool))
        {
            var curr = cvar.GetValue<bool>();
            Shared.Console.ExecuteCommand(cmd, new[] { cmdKey, (!curr).ToString() });
            return;
        }

        if (cmdKey.StartsWith('+'))
        {
            Shared.Console.ExecuteCommand(cmd, new[] { cmdKey, buttonIdentifier });
            return;
        }

        Shared.Console.ExecuteCommand(cmd, new[] { cmdKey });
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

    public static bool GetAction(InputAction action, [NotNullWhen(true)] out ActionState? state)
    {
        return _actions.TryGetValue(action, out state);
    }
}
