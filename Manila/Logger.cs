namespace Shiron.Manila.Utils;

using System.Collections;
using Spectre.Console;
using Microsoft.ClearScript;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shiron.Manila.Ext;

public static class Logger {
	private static bool verbose;
	private static bool quiet;

	public static void init(bool verbose, bool quiet) {
		Logger.verbose = verbose;
		Logger.quiet = quiet;
	}

	private static string formatMessage(object message) {
		if (message is object[] array)
			return string.Join(" ", array.Select(item => formatMessage(item)));

		if (message == null)
			return "null";

		// Handle JavaScript objects from ClearScript
		if (message is IScriptObject scriptObj) {
			try {
				return JsonConvert.SerializeObject(message, Formatting.None, new JsonSerializerSettings {
					ReferenceLoopHandling = ReferenceLoopHandling.Ignore
				});
			} catch (Exception e) {
				return $"<Unable to process JavaScript object: {e.Message}>";
			}
		}

		// Handle collections (arrays, lists, etc.)
		if (message is ICollection collection) {
			try {
				var jArray = JArray.FromObject(collection);
				return jArray.ToString(Formatting.Indented);
			} catch (Exception e) {
				return $"<Unable to serialize collection: {e.Message}>";
			}
		}

		// Handle exceptions with full stack trace
		if (message is Exception ex) {
			return $"Exception: {ex.GetType().Name}\nMessage: {ex.Message}\nStack Trace:\n{ex.StackTrace}";
		}

		// Handle other objects by attempting JSON serialization
		if (!(message is string) && !(message is ValueType)) {
			try {
				var jToken = JToken.FromObject(message);
				return jToken.ToString(Formatting.Indented);
			} catch {
				// Fallback to ToString() if serialization fails
				return message.ToString();
			}
		}

		// Handle simple types
		return message.ToString();
	}

	private static string escapeMarkup(string message) {
		return message
			.Replace("[", "[[")
			.Replace("]", "]]")
			.Replace("\n", "\n    "); // Indent multiline output
	}

	public enum LogLevel {
		Debug,
		Info,
		Warning,
		Error,
		Headless
	}

	public static void log(object[] message, LogLevel level, ManilaPlugin? plugin = null) {
		string formattedMessage = formatMessage(message);
		if (level == LogLevel.Headless) {
			Console.WriteLine(formattedMessage);
			return;
		}

		string color = level switch {
			LogLevel.Debug => "grey",
			LogLevel.Info => "white",
			LogLevel.Warning => "yellow",
			LogLevel.Error => "red",
			_ => "white"
		};
		string messageColor = level switch {
			LogLevel.Debug => "grey",
			LogLevel.Info => "white",
			LogLevel.Warning => "white",
			LogLevel.Error => "red",
			_ => "white"
		};

		var levelStr = escapeMarkup(level.ToString().ToUpper());
		string timestamp = DateTime.Now.ToString("hh:mm tt").ToLower();
		string pluginStr = plugin != null ? $"/{escapeMarkup($"{plugin.group}:{plugin.name}:{plugin.version}")}" : "";

		AnsiConsole.MarkupLine($"<[blue]{timestamp}[/]>[[[{color}]{levelStr}[/]{pluginStr}]]: [{messageColor}]{escapeMarkup(formattedMessage)}[/]");
	}

	public static void scriptLog(params object[] messages) {
		if (quiet) return;
		log(messages, LogLevel.Headless);
	}

	public static void info(params object[] messages) {
		if (!verbose || quiet) return;
		log(messages, LogLevel.Info);
	}

	public static void debug(params object[] messages) {
		if (!verbose || quiet) return;
		log(messages, LogLevel.Debug);
	}

	public static void warn(params object[] messages) {
		if (!verbose || quiet) return;
		log(messages, LogLevel.Warning);
	}

	public static void error(params object[] messages) {
		if (!verbose || quiet) return;
		log(messages, LogLevel.Error);
	}

	public static void pluginInfo(ManilaPlugin plugin, params object[] messages) {
		if (!verbose || quiet) return;
		log(messages, LogLevel.Info, plugin);
	}
	public static void pluginDebug(ManilaPlugin plugin, params object[] messages) {
		if (!verbose || quiet) return;
		log(messages, LogLevel.Debug, plugin);
	}

	public static void pluginWarn(ManilaPlugin plugin, params object[] messages) {
		if (!verbose || quiet) return;
		log(messages, LogLevel.Warning, plugin);
	}

	public static void pluginError(ManilaPlugin plugin, params object[] messages) {
		if (!verbose || quiet) return;
		log(messages, LogLevel.Error, plugin);
	}

	public static void print(params object[] messages) {
		if (quiet) return;
		log(messages, LogLevel.Headless);
	}
}
