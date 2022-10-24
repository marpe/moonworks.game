using MyGame.Utils;

namespace MyGame.TWConsole;

public class TWConsole
{
	public static event Action<StringBuilder>? OnCfgSave;
	private const string kCvarsFilename = "cvars.cfg";
	public readonly SortedDictionary<string, ConsoleCommand> Commands = new(StringComparer.InvariantCultureIgnoreCase);
	public readonly Dictionary<string, CVar> CVars = new();
	public readonly RingBuffer<string> CommandHistory = new(MAX_COMMAND_HISTORY_COUNT);
	public readonly ConsoleScreenBuffer ScreenBuffer;
	private const int MAX_COMMAND_HISTORY_COUNT = 48;
	public const int DEFAULT_WIDTH = 186;

	public TWConsole()
	{
		ScreenBuffer = new ConsoleScreenBuffer(DEFAULT_WIDTH, 180);
	}

	public void Initialize()
	{
		ProcessConsoleHandlerAttributes();
		ProcessCVarAttributes();
		Execute("exec " + kCvarsFilename, false);
	}

	private void ProcessCVarAttributes()
	{
		var asmList = new[]
		{
			Assembly.GetEntryAssembly()!,
		};

		foreach (var asm in asmList)
		{
			var bindingAttr = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
			                  BindingFlags.NonPublic;
			foreach (var type in asm.GetTypes())
			{
				foreach (var field in type.GetFields(bindingAttr))
				{
					var cvarAttribute = field.GetCustomAttribute<CVarAttribute>(false);

					if (cvarAttribute != null)
					{
						ProcessCVar(field, cvarAttribute);
					}
				}

				foreach (var prop in type.GetProperties(bindingAttr))
				{
					var cvarAttribute = prop.GetCustomAttribute<CVarAttribute>(false);

					if (cvarAttribute != null)
					{
						ProcessCVar(prop, cvarAttribute);
					}
				}
			}
		}
	}

	private void ProcessCVar(PropertyInfo prop, CVarAttribute cvarAttribute)
	{
		if (prop.GetMethod == null || !prop.GetMethod.IsStatic ||
		    prop.SetMethod == null || !prop.SetMethod.IsStatic)
		{
			throw new InvalidOperationException(
				$"Cannot use CVar attribute on non-static property: {prop.DeclaringType?.Name}.{prop.Name}");
		}

		var cvar = new CVar(cvarAttribute.Name, prop);
		RegisterCVar(cvar, cvarAttribute);
	}

	private void ProcessCVar(FieldInfo field, CVarAttribute cvarAttribute)
	{
		if (!field.IsStatic)
		{
			throw new InvalidOperationException(
				$"Cannot use CVar attribute on non-static fields: {field.DeclaringType?.Name}.{field.Name}");
		}

		var cvar = new CVar(cvarAttribute.Name, field);

		RegisterCVar(cvar, cvarAttribute);
	}

	private static string Colorize(Type type, string? value)
	{
		if (value == null)
			return "^8null^0";
		var formatString = value.ToLower() switch
		{
			"true" => "^7{0}^0",
			"false" => "^4{0}^0",
			_ => "^0{0}"
		};
		return string.Format(formatString, value);
	}

	private void RegisterCVar(CVar cvar, CVarAttribute cvarAttribute)
	{
		var handler = new ConsoleCommand.ConsoleCommandHandler((console, cmd, args) =>
		{
			/*if (args.Length == 1 && cvar.VarType == typeof(bool))
			{
				cvar.SetValue(!cvar.GetValue<bool>());
			}
			else */
			if (args.Length > 1)
			{
				try
				{
					cvar.SetValue(args[1]);
				}
				catch (Exception e)
				{
					Print($"Error: {e.Message}");
				}
			}

			Print($"{cmd.Key} = \"{Colorize(cvar.VarType, cvar.GetValue())}^2\"");
		});

		var typeName = ConsoleUtils.GetDisplayName(cvar.VarType);
		var defaultValue = ConsoleUtils.ConvertToString(cvar.DefaultValue);

		RegisterCommand(
			cvarAttribute.Name,
			$"{cvarAttribute.Description} <^3{typeName}^0> ({Colorize(cvar.VarType, defaultValue)})",
			handler
		);

		CVars.Add(cvar.Key, cvar);
	}

	private void ProcessConsoleHandlerAttributes()
	{
		var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
		foreach (var mi in GetType().GetMethods(bindingFlags))
		{
			var attrs = mi.GetCustomAttributes<ConsoleHandlerAttribute>(false);
			foreach (var attr in attrs)
			{
				ProcessHandlerMethod(mi, attr);
			}
		}

		var asm = Assembly.GetEntryAssembly()!;

		foreach (var type in asm.DefinedTypes)
		{
			foreach (var method in type.DeclaredMethods)
			{
				var attrs = method.GetCustomAttributes<ConsoleHandlerAttribute>(false);
				foreach (var attr in attrs)
				{
					ProcessHandlerMethod(method, attr);
				}
			}
		}
	}

	private void ProcessHandlerMethod(MethodInfo method, ConsoleHandlerAttribute attr)
	{
		var parameters = method.GetParameters();

		var defaults = new object?[parameters.Length];

		for (var i = 0; i < parameters.Length; i++)
		{
			var param = parameters[i];

			if (!ConsoleUtils.CanParse(param.ParameterType))
			{
				throw new InvalidOperationException("Invalid parameter type " + param.ParameterType.Name);
			}

			if (param.HasDefaultValue)
			{
				defaults[i] = param.DefaultValue;
			}
			else if (param.ParameterType == typeof(string))
			{
				defaults[i] = string.Empty;
			}
		}

		RegisterCommand(new ConsoleCommand(attr.Command, attr.Description,
			(console, cmd, args) =>
			{
				if (parameters.Length == 0)
				{
					method.Invoke(null, null);
				}
				else
				{
					var p = (object?[])defaults.Clone();
					for (var i = 0; i < p.Length && i < args.Length - 1; i++)
					{
						// args[0] will be the command
						p[i] = ConsoleUtils.ParseArg(parameters[i].ParameterType, args[i + 1]);
					}

					try
					{
						method.Invoke(null, p);
					}
					catch (Exception e)
					{
						Print(e.ToString());
					}
				}
			}));
	}

	[ConsoleHandler("sysinfo", "Display system info")]
	private void DisplaySystemInfo()
	{
		Print("Name         : " + Environment.MachineName);
		Print("OSVer        : " + Environment.OSVersion);
		Print("64BitOS      : " + Environment.Is64BitOperatingSystem);
		Print("64BitProcess : " + Environment.Is64BitProcess);
		Print("PageFile     : " + Environment.SystemPageSize);
		Print("CPUs         : " + Environment.ProcessorCount);
		Print("CLRVer       : " + Environment.Version);
	}

	[ConsoleHandler("mem", "Displays memory usage")]
	private void DisplayMemoryUsage()
	{
		Print("WSMem: " + Environment.WorkingSet);
		Print("GCC1 : " + GC.CollectionCount(0));
		Print("GCC2 : " + GC.CollectionCount(1));
		Print("GCC3 : " + GC.CollectionCount(2));
		Print("Total: " + GC.GetTotalMemory(false));
	}

	[ConsoleHandler("gc", "Immediately perform full garbage collection")]
	private void PerformGarbageCollection()
	{
		Print("Forcing Garbage Collection...");
		var now = DateTime.Now;
		GC.Collect(3, GCCollectionMode.Forced);
		GC.Collect(2, GCCollectionMode.Forced);
		GC.Collect(1, GCCollectionMode.Forced);
		Print("Garbage Collection took ~" + DateTime.Now.Subtract(now).TotalMilliseconds + "ms");
	}

	[ConsoleHandler("res", "Print display resolution")]
	private void PrintDisplayResolution()
	{
		var size = Shared.MainWindow.Size;
		Print($"Current resolution: {size.X}x{size.Y}");
	}

	public ConsoleCommand RegisterCommand(string command, string description,
		ConsoleCommand.ConsoleCommandHandler handler)
	{
		var consoleCommand = new ConsoleCommand(command, description, handler);
		RegisterCommand(consoleCommand);
		return consoleCommand;
	}

	public void RegisterCommand(ConsoleCommand command)
	{
		Commands.Add(command.Key, command);
	}

	public void RegisterCommands(IEnumerable<ConsoleCommand> commands)
	{
		foreach (var command in commands)
		{
			RegisterCommand(command);
		}
	}

	public void UnregisterCommand(string key)
	{
		if (Commands.ContainsKey(key))
		{
			Commands.Remove(key);
		}
	}

	public void UnregisterCommands(IEnumerable<string> keys)
	{
		foreach (var key in keys)
		{
			UnregisterCommand(key);
		}
	}

	[ConsoleHandler("con_colors", "Print colors")]
	private void ColorsCommand()
	{
		var indices = Enumerable.Range(0, 10);
		var strings = indices.Select(i => $"^{i} {i} ");
		Print(string.Join("\n", strings));
	}

	[ConsoleHandler("con_clear", "Clear console")]
	private void ClearCommand()
	{
		ScreenBuffer.Clear();
	}

	[ConsoleHandler("history", "Print command history")]
	private void HistoryCommand()
	{
		var history = new List<string>(CommandHistory);
		history.Reverse();
		Print(string.Join("\n", history));
	}

	[ConsoleHandler("echo", "Prints input to console")]
	private void EchoCommand(string text)
	{
		Print(string.Join(" ", text));
	}

	[ConsoleHandler("help", "Lists available console commands")]
	private void HelpCommand(string search = "")
	{
		string FormatCommand(ConsoleCommand c)
		{
			var cmdArgs = c.Arguments.Count > 0 ? " [" + string.Join(", ", c.Arguments) + "]" : string.Empty;
			return $"^6{c.Key}{cmdArgs}^0: {c.Description}";
		}

		StringBuilder sb = new();
		if (string.IsNullOrWhiteSpace(search))
		{
			sb.AppendLine("^8Available commands:");
			foreach (var (_, value) in Commands)
			{
				sb.AppendLine(FormatCommand(value));
			}
		}
		else
		{
			var results = Commands.Where(c => c.Key.Contains(search)).Select(c => c.Value).ToList();
			if (results.Count == 0)
			{
				sb.AppendLine($"^4Could not find any commands which contains {search}");
			}
			else
			{
				sb.AppendLine($"^8Found {results.Count} commands containing {search}:");
				foreach (var c in results)
				{
					sb.AppendLine(FormatCommand(c));
				}
			}
		}

		Print(sb.ToString());
	}

	public void Print(ReadOnlySpan<char> text)
	{
		ScreenBuffer.AddLine(text);
	}

	public void Execute(ReadOnlySpan<char> text, bool addToHistory = true)
	{
		if (text.Length == 0)
			return;

		if (addToHistory)
		{
			Print(text);
		}

		var trimmed = text.Trim();

		if (addToHistory)
		{
			if (CommandHistory.Count == 0 || CommandHistory[0] != trimmed)
			{
				CommandHistory.Add(trimmed.ToString());
			}
		}

		var args = ConsoleUtils.SplitArgs(trimmed);

		var commandPart = args[0];

		if (Commands.ContainsKey(commandPart))
		{
			Commands[commandPart].Handler.Invoke(this, Commands[commandPart], args);
		}
		else
		{
			Print($"^4Command not found: {commandPart}");
		}
	}

	[ConsoleHandler("cfg_save", "Save config file")]
	private void CfgSave(string filename = "cvars.cfg")
	{
		var sb = new StringBuilder();
		foreach (var (key, value) in CVars)
		{
			sb.AppendLine(key + " \"" + value.GetValue() + "\"");
		}

		OnCfgSave?.Invoke(sb);

		File.WriteAllText(filename, sb.ToString());

		Print("Saved config to " + filename);
	}

	[ConsoleHandler("exec", "Loads a config file from disk")]
	private void Exec(string filename)
	{
		if (File.Exists(filename))
		{
			var cvars = File.ReadAllLines(filename);
			foreach (var cvar in cvars)
			{
				Execute(cvar, false);
			}

			Print($"Loaded config from {filename}");
		}
		else
		{
			Print($"File not found {filename}");
		}
	}

	public void SaveCVars()
	{
		Execute("cfg_save " + kCvarsFilename, false);
	}
}
