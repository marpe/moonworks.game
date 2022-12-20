using System.Diagnostics.CodeAnalysis;

namespace MyGame.Input;

public static class Binds
{
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
        public bool WasActive;
        public ulong GameUpdateCount;
        public ulong WorldUpdateCount;
    }
    
    public static class Player
    {
        public static PlayerCommand ToPlayerCommand()
        {
            var cmd = new PlayerCommand();

            var right = _actions[InputAction.Right];
            var left = _actions[InputAction.Left];
            var fire1 = _actions[InputAction.Fire1];
            var respawn = _actions[InputAction.Respawn];
            var jump = _actions[InputAction.Jump];
            var moveToMouse = _actions[InputAction.MoveToMouse];
            
            if (right.Active)
                cmd.MovementX += 1;

            if (left.Active)
                cmd.MovementX += -1;

            cmd.IsFiring = (!fire1.WasActive && fire1.WorldUpdateCount == Shared.Game.World.WorldUpdateCount - 1) && fire1.Active;
            cmd.Respawn = (!respawn.WasActive && respawn.WorldUpdateCount == Shared.Game.World.WorldUpdateCount - 1) && respawn.Active;
            cmd.IsJumpDown = jump.Active;
            cmd.IsJumpPressed = (!jump.WasActive && jump.WorldUpdateCount == Shared.Game.World.WorldUpdateCount - 1) && jump.Active;
            cmd.MoveToMouse = moveToMouse.Active;

            return cmd;
        }
    }

    public const int MouseWheelUp = 0;
    public const int MouseWheelDown = 1;

    private static readonly Dictionary<string, string> _binds = new(StringComparer.InvariantCultureIgnoreCase);

    public static Dictionary<InputAction, ActionState> _actions = new();

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
            ("cam_zoom_in", "Increase camera zoom", InputAction.ZoomIn),
            ("cam_zoom_out", "Decrease camera zoom", InputAction.ZoomOut),
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
            bind.WasActive = bind.Active;
            bind.Active = true;
            bind.Sources[0] = args.Length > 1 ? args[1] : "";
            bind.GameUpdateCount = Shared.Game.Time.UpdateCount;
            bind.WorldUpdateCount = Shared.Game.World.WorldUpdateCount;
        };
        ConsoleCommand.ConsoleCommandHandler upHandler = (console, cmd, args) =>
        {
            bind.WasActive = bind.Active;
            bind.Active = false;
            bind.Sources[0] = args.Length > 1 ? args[1] : "";
            bind.GameUpdateCount = Shared.Game.Time.UpdateCount;
            bind.WorldUpdateCount = Shared.Game.World.WorldUpdateCount;
        };
        var downCmd = new ConsoleCommand($"+{cmdName}", description.ToString(), downHandler, Array.Empty<ConsoleCommandArg>(), Array.Empty<string>(), false);
        var upCmd = new ConsoleCommand($"-{cmdName}", description.ToString(), upHandler, Array.Empty<ConsoleCommandArg>(), Array.Empty<string>(), false);
        Shared.Console.RegisterCommand(downCmd);
        Shared.Console.RegisterCommand(upCmd);
    }

    private static HashSet<int> _buttonsToClear = new();

    private static Dictionary<int, ActionState> _buttons = new();
    public static IReadOnlyDictionary<int, ActionState> Buttons => _buttons;

    private const int MOUSE_WHEEL_OFFSET = 200;
    private const int MOUSE_BUTTON_OFFSET = 100;

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

    public static void UpdateButtonStates(in InputState inputState)
    {
        void UpdateButton(ActionState button, bool isActive)
        {
            button.WasActive = button.Active;
            button.Active = isActive;
            button.GameUpdateCount = Shared.Game.Time.UpdateCount;
        }

        foreach (var key in inputState.KeysDown)
        {
            var buttonId = (int)key;
            if (!_buttons.ContainsKey(buttonId))
                _buttons[buttonId] = new ActionState();
            UpdateButton(_buttons[buttonId], true);
        }

        foreach (var mouseButton in inputState.MouseButtonsDown)
        {
            var buttonId = (int)mouseButton + 100;
            if (!_buttons.ContainsKey(buttonId))
                _buttons[buttonId] = new ActionState();
            UpdateButton(_buttons[buttonId], true);
        }

        var mwheelUpId = MouseWheelUp + MOUSE_WHEEL_OFFSET;
        var mwheelDownId = MouseWheelDown + MOUSE_WHEEL_OFFSET;

        void UpdateWheel(int wheelId, bool isActive)
        {
            if (_buttons.TryGetValue(wheelId, out var wheel))
            {
                if (wheel.Active != isActive)
                    UpdateButton(wheel, isActive);
                return;
            }

            if (!isActive)
                return;

            _buttons[wheelId] = new ActionState();
            UpdateButton(_buttons[wheelId], true);
        }

        UpdateWheel(mwheelUpId, inputState.MouseWheelDelta > 0);
        UpdateWheel(mwheelDownId, inputState.MouseWheelDelta < 0);

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
    }

    public static void ExecuteTriggeredBinds()
    {
        // iterate through all mouse/keycodes, check if any binds are assigned to them
        // and if so, execute the command if the button has been pressed (or released for "-" actions)
        foreach (var (code, identifier) in MouseWheelCodeToName)
        {
            // offset the code since that's the "button id" used in the _buttons dictionary
            HandleButtonBind(code + MOUSE_WHEEL_OFFSET, identifier);
        }

        foreach (var (code, identifier) in MouseButtonCodeToName)
        {
            // offset the code since that's the "button id" used in the _buttons dictionary
            HandleButtonBind((int)code + MOUSE_BUTTON_OFFSET, identifier);
        }

        foreach (var (code, identifier) in KeyCodeToName)
        {
            // console key handled separately
            if (code == KeyCode.Grave && IsButtonPressed((int)code))
            {
                ConsoleScreen.ToggleConsole();
                continue;
            }

            // keyboard codes map directly to "button id's" so no offset applied here 
            HandleButtonBind((int)code, identifier);
        }
    }

    /// <summary>
    /// Pulls the bind identified by "buttonIdentifier", interprets the command string and executes it 
    /// </summary>
    /// <param name="buttonId">An id corresponding to the keyboard, mouse or gamepad button being checked</param>
    /// <param name="buttonIdentifier">The buttons identifier, e.g mwheelup, kp_enter or gamepad_xyz</param>
    private static void HandleButtonBind(int buttonId, string buttonIdentifier)
    {
        if (!TryGetBind(buttonIdentifier, out var bindStr))
            return;

        var split = ConsoleUtils.SplitArgs(bindStr);
        var cmdKey = split[0];
        if (!Shared.Console.Commands.TryGetValue(cmdKey, out var cmd))
        {
            Logs.LogError($"Command not found: {buttonIdentifier} -> {cmdKey}");
            return;
        }

        if (IsButtonReleased(buttonId))
        {
            // key up events only trigger binds if the binding is a button command (leading + sign)
            if (!cmdKey.StartsWith('+'))
                return;

            var upCmdKey = $"-{cmdKey.AsSpan().Slice(1)}";
            var upCmd = Shared.Console.Commands[upCmdKey];
            Shared.Console.ExecuteCommand(upCmd, new[] { cmdKey, buttonIdentifier });
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
                Shared.Console.ExecuteCommand(cmd, new[] { cmdKey, buttonIdentifier });
                return;
            }

            Shared.Console.ExecuteCommand(cmd, split);
            return;
        }
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

    public static ActionState GetAction(InputAction action)
    {
        return _actions[action];
    }
}
