using MyGame.Utils;

namespace MyGame.TWConsole;

public class TWConsole
{
	public static event Action<StringBuilder>? OnCfgSave;
	private const string kCvarsFilename = "cvars.cfg";
	public readonly SortedDictionary<string, ConsoleCommand> Commands = new(StringComparer.InvariantCultureIgnoreCase);
	public readonly SortedDictionary<string, ConsoleCommand> Aliases = new(StringComparer.InvariantCultureIgnoreCase);
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

	private static string Colorize(object? value)
	{
		if (value == null)
			return "^8null^0";
		
		if (value is bool boolValue)
			return boolValue ? "^7true" : "^4false";

		if (value is string strValue)
			return $"\"{strValue}\"";

		return ConsoleUtils.ConvertToString(value);
	}
	
	private void RegisterCVar(CVar cvar, CVarAttribute cvarAttribute)
	{
		var typeName = ConsoleUtils.GetDisplayName(cvar.VarType);
		var defaultValueStr = Colorize(cvar.DefaultValue);

		RegisterCommand(
			new ConsoleCommand(
				cvarAttribute.Name,
				$"{cvarAttribute.Description} <^3{typeName}^0> ({defaultValueStr})",
				CVarHandler(cvar),
				new ConsoleCommandArg[]
				{
					new(cvar.Key, true, cvar.DefaultValue, cvar.VarType)
				},
				Array.Empty<string>(),
				true
			)
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

		var defaults = new ConsoleCommandArg[parameters.Length];

		for (var i = 0; i < parameters.Length; i++)
		{
			var param = parameters[i];

			if (!ConsoleUtils.CanParse(param.ParameterType))
			{
				throw new InvalidOperationException($"Invalid parameter type: {param.ParameterType.Name}");
			}

			defaults[i] = new ConsoleCommandArg(param.Name, param.HasDefaultValue, param.DefaultValue, param.ParameterType);
		}

		RegisterCommand(
			new ConsoleCommand(
				attr.Command,
				attr.Description,
				ConsoleCommandHandler(method),
				defaults,
				attr.Aliases,
				false
			)
		);
	}

	private static ConsoleCommand.ConsoleCommandHandler CVarHandler(CVar cvar)
	{
		return (console, cmd, args) =>
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
					console.Print($"^4Error: {e}");
				}
			}

			console.Print($"{cmd.Key} = {Colorize(cvar.GetValueRaw())}");
		};
	}
	
	private static ConsoleCommand.ConsoleCommandHandler ConsoleCommandHandler(MethodBase method)
	{
		return (console, cmd, args) =>
		{
			try
			{
				var numRequiredParams = cmd.Arguments.Count(x => !x.HasDefaultValue);
				var numSuppliedParams = args.Length - 1;
				
				if (numRequiredParams > numSuppliedParams)
				{
					console.Print($"Usage:\n{FormatCommand(cmd)}");
					return;
				}
				
				var parameters = cmd.Arguments.Select(x => x.DefaultValue).ToArray();

				for (var i = 0; i < parameters.Length && i < numSuppliedParams; i++)
				{
					// args[0] will be the command
					parameters[i] = ConsoleUtils.ParseArg(cmd.Arguments[i].Type, args[i + 1]);
				}

				method.Invoke(console, parameters);
			}
			catch (Exception e)
			{
				console.Print($"^4Error: {e}");
			}
		};
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

	private void RegisterCommand(ConsoleCommand command)
	{
		Commands.Add(command.Key, command);
		foreach (var alias in command.Aliases)
		{
			Aliases.Add(alias, command);
		}
	}

	private void UnregisterCommand(string key)
	{
		if (Commands.ContainsKey(key))
		{
			var command = Commands[key];
			Commands.Remove(key);
			foreach (var alias in command.Aliases)
				Aliases.Remove(alias);
		}
	}

	[ConsoleHandler("con.colors", "Print colors", new [] { "colors" })]
	private void ColorsCommand()
	{
		var indices = Enumerable.Range(0, 10);
		var strings = indices.Select(i => $"^{i} {i} ");
		Print(string.Join("\n", strings));
	}

	[ConsoleHandler("con.clear", "Clear console", new[] { "cls", "clear" })]
	private void ClearCommand()
	{
		ScreenBuffer.Clear();
	}

	[ConsoleHandler("history", "Print command history")]
	private void HistoryCommand()
	{
		var sb = new StringBuilder();
		for (var i = 0; i < CommandHistory.Count; i++)
		{
			sb.AppendLine(CommandHistory[i]);
		}
		Print(sb.ToString());
	}

	[ConsoleHandler("echo", "Prints input to console")]
	private void EchoCommand(string text)
	{
		Print(string.Join(" ", text));
	}

	private static string FormatCommand(ConsoleCommand cmd)
	{
		var cmdArgs = string.Empty;
		if (cmd.Arguments.Length > 0)
		{
			// TODO (marpe): Clean up this horrible mess
			var formattedArgs = cmd.Arguments.Select(x =>
			{
				var str = x.Name + " (^3" + ConsoleUtils.GetDisplayName(x.Type) + "^1";
				if (x.HasDefaultValue)
					str += ", " + Colorize(x.DefaultValue);
				str += ")";
				return str;
			});
			var args = string.Join(", ", formattedArgs);
			cmdArgs = $" ^1[{args}]^0";
		}

		var cmdPart = cmd.Key;
		if (cmd.Aliases.Length > 0)
		{
			cmdPart += " ^5(" + string.Join(", ", cmd.Aliases) + ")^0";
		}

		return $"^6{cmdPart}{cmdArgs}^0: {cmd.Description}";
	}

	
	[ConsoleHandler("help", "Lists available console commands and cvars")]
	private void HelpCommand(string search = "")
	{
		var sb = new StringBuilder();
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

	[ConsoleHandler("commands", "Lists available console commands")]
	private void ListCommands(string search = "")
	{
		var sb = new StringBuilder();
		if (string.IsNullOrWhiteSpace(search))
		{
			sb.AppendLine("^8Available commands:");
			var results = Commands.Where(c => !c.Value.IsCVar);
			foreach (var (_, value) in Commands)
			{
				sb.AppendLine(FormatCommand(value));
			}
		}
		else
		{
			var results = Commands.Where(c => c.Key.Contains(search) && !c.Value.IsCVar).Select(c => c.Value).ToList();
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

	[ConsoleHandler("cvars", "List available cvars")]
	private void CVarsCommand(string search = "")
	{
		var sb = new StringBuilder();
		if (string.IsNullOrWhiteSpace(search))
		{
			var results = Commands.Where(c => c.Value.IsCVar).ToList();
			if (results.Count == 0)
			{
				sb.AppendLine($"There are no cvars registered");
			}
			else
			{
				sb.AppendLine("^8Available cvars:");
				foreach (var (_, value) in results)
				{
					sb.AppendLine(FormatCommand(value));
				}
			}
		}
		else
		{
			var results = Commands.Where(c => c.Key.Contains(search) && c.Value.IsCVar).Select(c => c.Value).ToList();
			if (results.Count == 0)
			{
				sb.AppendLine($"^4Could not find any cvars which contains {search}");
			}
			else
			{
				sb.AppendLine($"^8Found {results.Count} cvars containing {search}:");
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
		else if (Aliases.ContainsKey(commandPart))
		{
			Aliases[commandPart].Handler.Invoke(this, Aliases[commandPart], args);
		}
		else
		{
			Print($"^4Command not found: {commandPart}");
		}
	}

	[ConsoleHandler("quit", "Quit the game", new[] { "exit" })]
	private void QuitCommand()
	{
		Shared.Game.Quit();
	}

	[ConsoleHandler("cfg.save", "Save config file")]
	private void CfgSave(string filename = "cvars.cfg")
	{
		var sb = new StringBuilder();
		foreach (var (key, value) in CVars)
		{
			sb.AppendLine($"{key} \"{value.GetStringValue()}\"");
		}

		OnCfgSave?.Invoke(sb);

		File.WriteAllText(filename, sb.ToString());

		Print($"Saved config to {filename}");
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
		Execute("cfg.save " + kCvarsFilename, false);
	}
}
