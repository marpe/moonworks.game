using System.Diagnostics.CodeAnalysis;

namespace MyGame.Input;

public static class Binds
{
    private static Dictionary<string, string> _binds = new(StringComparer.InvariantCultureIgnoreCase);

    static Binds()
    {
        TWConsole.TWConsole.OnCfgSave += builder =>
        {
            builder.AppendLine("unbindall");
            var sb = GetBindsAsText();
            builder.Append(sb);
        };
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

    private static bool TryParseKey(string keyStr, out KeyCode keyCode)
    {
        var parsedKey = Enum.TryParse(keyStr, true, out keyCode);
        if (parsedKey)
            return true;

        Shared.Console.Print($"Invalid key name: {keyStr}");
        return false;
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

    private static bool TryParseMouseButton(ReadOnlySpan<char> keyStr, out int mouseButton)
    {
        if (keyStr.Length == 3 && int.TryParse(keyStr.Slice(2), out mouseButton) && mouseButton is >= 1 and <= 5)
            return true;

        mouseButton = -1;
        Shared.Console.Print("Invalid mouse button name, valid values are: mb<1-5>");
        return false;
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

        _binds[keyStr] = cmdStr;
    }

    [ConsoleHandler("unbind", "Unbind a key")]
    public static void Unbind(string keyStr)
    {
        _binds.Remove(keyStr);
    }

    private static StringBuilder GetBindsAsText()
    {
        var sb = new StringBuilder();

        List<(string key, string action)> binds = new();

        foreach (var (key, cmd) in _binds)
        {
            binds.Add((key, cmd));
        }

        foreach (var (key, action) in binds)
        {
            sb.AppendLine($"bind {key.ToLowerInvariant()} \"{action.ToLowerInvariant()}\"");
        }

        return sb;
    }
}
