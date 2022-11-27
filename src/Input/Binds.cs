using System.Diagnostics.CodeAnalysis;
using MyGame.Cameras;

namespace MyGame.Input;

public static class Binds
{
    private static readonly Dictionary<string, string> _binds = new(StringComparer.InvariantCultureIgnoreCase);

    public static readonly Dictionary<MouseButtonCode, string> MouseButtonCodeToName;

    private static readonly Dictionary<string, MouseButtonCode> _mouseButtonNames = new()
    {
        { "mb_left", MouseButtonCode.Left },
        { "mb_right", MouseButtonCode.Right },
        { "mb_middle", MouseButtonCode.Middle },
        { "mb_x1", MouseButtonCode.X1 },
        { "mb_x2", MouseButtonCode.X2 },
    };

    static Binds()
    {
        MouseButtonCodeToName = _mouseButtonNames.ToDictionary(x => x.Value, x => x.Key);
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
            ("pageup", "speed_up"),
            ("pagedown", "speed_down"),
            ("p", "pause"),
            ("mb_left", "+fire1"),
            ("d1", "save_pos"),
            ("d2", "load_pos")
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
            ("cam_up", "Move camera up", KeyCode.I, CameraBinds.Up),
            ("cam_down", "Move camera down", KeyCode.K, CameraBinds.Down),
            ("cam_left", "Move camera left", KeyCode.J, CameraBinds.Left),
            ("cam_right", "Move camera right", KeyCode.L, CameraBinds.Right),
        };

        var mouseBinds = new[]
        {
            ("mb_middle", "cam_pan", "Pan camera", CameraBinds.Pan)
        };

        for (var i = 0; i < defaultBinds.Length; i++)
        {
            var (key, cmd) = defaultBinds[i];
            Bind(key, cmd);
        }

        for (var i = 0; i < keyBinds.Length; i++)
        {
            var (cmd, description, defaultBind, bind) = keyBinds[i];
            Bind(defaultBind.ToString(), $"+{cmd}");
            RegisterConsoleCommandForBind(cmd, description, bind);
        }

        for (var i = 0; i < mouseBinds.Length; i++)
        {
            var (button, cmd, description, bind) = mouseBinds[i];
            Bind(button, $"+{cmd}");
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

    public static bool TryGetBind(ReadOnlySpan<char> key, [NotNullWhen(true)] out string? bind)
    {
        var keyStr = key.ToString();
        if (_binds.ContainsKey(keyStr))
        {
            bind = _binds[keyStr];
            return true;
        }

        bind = null;
        return false;
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

        var isValidKeyStr = Enum.TryParse<KeyCode>(keyStr, true, out _) || _mouseButtonNames.ContainsKey(keyStr);

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
            sb.AppendLine($"bind {key.ToLowerInvariant(),-20} \"{cmd.ToLowerInvariant()}\"");
        }

        return sb;
    }
}
