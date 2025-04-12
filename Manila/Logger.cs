using System.Collections;
using Spectre.Console;
using Microsoft.ClearScript;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shiron.Manila.Attributes;

namespace Shiron.Manila.Utils;

/// <summary>
/// The internal logger for Manila. Plugins should use their own logger:
/// See <see cref="PluginInfo(Attributes.ManilaPlugin, object[])"/> as an example.
/// </summary>
public static class Logger {
    private static bool verbose;
    private static bool quiet;

    /// <summary>
    /// Initializes the logger.
    /// </summary>
    /// <param name="verbose">Enables verbose logging</param>
    /// <param name="quiet">Disables logging</param>
    /// <exception cref="ArgumentException">Cannot enable both verbose and quiet logging</exception>
    public static void Init(bool verbose, bool quiet) {
        if (verbose && quiet) throw new ArgumentException("Cannot enable both verbose and quiet logging");

        Logger.verbose = verbose;
        Logger.quiet = quiet;
    }

    /// <summary>
    /// Formats a message for logging.
    /// </summary>
    /// <param name="message">The message</param>
    /// <returns>A formatted string representation of the message</returns>
    internal static string FormatMessage(object message) {
        if (message is object[] array)
            return string.Join(" ", array.Select(item => FormatMessage(item)));

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

    /// <summary>
    /// Escapes markup characters in a message for console output.
    /// </summary>
    /// <param name="message">The message to escape</param>
    /// <returns>The escaped message with proper indentation for multiline output</returns>
    internal static string EscapeMarkup(string message) {
        return message
            .Replace("[", "[[")
            .Replace("]", "]]")
            .Replace("\n", "\n    "); // Indent multiline output
    }

    /// <summary>
    /// Defines the log levels used by the logger.
    /// </summary>
    internal enum LogLevel {
        /// <summary>Debug level for detailed troubleshooting information</summary>
        Debug,
        /// <summary>Info level for general information</summary>
        Info,
        /// <summary>Warning level for potential issues</summary>
        Warning,
        /// <summary>Error level for error conditions</summary>
        Error,
        /// <summary>Headless level for plain console output without formatting</summary>
        Headless
    }

    /// <summary>
    /// Logs a message to the console.
    /// </summary>
    /// <param name="message">The message to log</param>
    /// <param name="level">The log level</param>
    /// <param name="plugin">Optional plugin information to include in the log message</param>
    internal static void Log(object[] message, LogLevel level, ManilaPlugin? plugin = null) {
        string formattedMessage = FormatMessage(message);
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

        var levelStr = EscapeMarkup(level.ToString().ToUpper());
        string timestamp = DateTime.Now.ToString("hh:mm tt").ToLower();
        string pluginStr = plugin != null ? $"/{EscapeMarkup($"{plugin.Group}:{plugin.Name}@{plugin.Version}")}" : "";

        AnsiConsole.MarkupLine($"<[blue]{timestamp}[/]>[[[{color}]{levelStr}[/]{pluginStr}]]: [{messageColor}]{EscapeMarkup(formattedMessage)}[/]");
    }

    /// <summary>
    /// Logs an informational message to the console.
    /// Only logs if verbose mode is enabled and quiet mode is disabled.
    /// </summary>
    /// <param name="messages">The messages to log</param>
    public static void Info(params object[] messages) {
        if (!verbose || quiet) return;
        Log(messages, LogLevel.Info);
    }

    /// <summary>
    /// Logs a debug message to the console.
    /// Only logs if verbose mode is enabled and quiet mode is disabled.
    /// </summary>
    /// <param name="messages">The messages to log</param>
    public static void Debug(params object[] messages) {
        if (!verbose || quiet) return;
        Log(messages, LogLevel.Debug);
    }

    /// <summary>
    /// Logs a warning message to the console.
    /// Only logs if verbose mode is enabled and quiet mode is disabled.
    /// </summary>
    /// <param name="messages">The messages to log</param>
    public static void Warn(params object[] messages) {
        if (!verbose || quiet) return;
        Log(messages, LogLevel.Warning);
    }

    /// <summary>
    /// Logs an error message to the console.
    /// Only logs if verbose mode is enabled and quiet mode is disabled.
    /// </summary>
    /// <param name="messages">The messages to log</param>
    public static void Error(params object[] messages) {
        if (!verbose || quiet) return;
        Log(messages, LogLevel.Error);
    }

    /// <summary>
    /// Logs an informational message from a plugin to the console.
    /// Only logs if verbose mode is enabled and quiet mode is disabled.
    /// </summary>
    /// <param name="plugin">The plugin that is logging the message</param>
    /// <param name="messages">The messages to log</param>
    public static void PluginInfo(ManilaPlugin plugin, params object[] messages) {
        if (!verbose || quiet) return;
        Log(messages, LogLevel.Info, plugin);
    }

    /// <summary>
    /// Logs a debug message from a plugin to the console.
    /// Only logs if verbose mode is enabled and quiet mode is disabled.
    /// </summary>
    /// <param name="plugin">The plugin that is logging the message</param>
    /// <param name="messages">The messages to log</param>
    public static void PluginDebug(ManilaPlugin plugin, params object[] messages) {
        if (!verbose || quiet) return;
        Log(messages, LogLevel.Debug, plugin);
    }

    /// <summary>
    /// Logs a warning message from a plugin to the console.
    /// Only logs if verbose mode is enabled and quiet mode is disabled.
    /// </summary>
    /// <param name="plugin">The plugin that is logging the message</param>
    /// <param name="messages">The messages to log</param>
    public static void PluginWarn(ManilaPlugin plugin, params object[] messages) {
        if (!verbose || quiet) return;
        Log(messages, LogLevel.Warning, plugin);
    }

    /// <summary>
    /// Logs an error message from a plugin to the console.
    /// Only logs if verbose mode is enabled and quiet mode is disabled.
    /// </summary>
    /// <param name="plugin">The plugin that is logging the message</param>
    /// <param name="messages">The messages to log</param>
    public static void PluginError(ManilaPlugin plugin, params object[] messages) {
        if (!verbose || quiet) return;
        Log(messages, LogLevel.Error, plugin);
    }

    /// <summary>
    /// Prints a message directly to the console without formatting.
    /// Only logs if quiet mode is disabled.
    /// </summary>
    /// <param name="messages">The messages to print</param>
    public static void Print(params object[] messages) {
        if (quiet) return;
        Log(messages, LogLevel.Headless);
    }
}
