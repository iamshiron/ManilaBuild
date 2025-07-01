
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.Logging;

public static class StdOutSink {
    public static void Init(bool verbose, bool quiet, bool structured) {
        var jsonSerializerSettings = new JsonSerializerSettings {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            Converters = { new StringEnumConverter(), new LogEntryConverter() },
            TypeNameHandling = TypeNameHandling.Auto,
            NullValueHandling = NullValueHandling.Ignore
        };

        Logger.OnLogEntry += entry => {
            if (structured) {
                Console.WriteLine(JsonConvert.SerializeObject(entry, jsonSerializerSettings));
                return;
            }

            // When quiet is set, only log errors and above
            if (quiet) {
                if (entry.Level >= LogLevel.Error)
                    RenderLog(entry, verbose);
                return;
            }
            RenderLog(entry, verbose);
        };
    }

    public static void RenderLog(ILogEntry e, bool verbose) {
        // Only log info and higher logs while verbose mode is disabled
        if (!verbose && e.Level < LogLevel.Info) return;

        var prefix = verbose ? $"<{DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).ToString("yyyy-MM-dd HH:mm:ss")}>[{e.Level.ToString().ToUpperInvariant()}]: " : "";

        switch (e) {
            case BasicLogEntry entry:
                Console.WriteLine($"{prefix}{entry.Message}");
                break;
            case BasicPluginLogEntry entry:
                Console.WriteLine($"{prefix}[{entry.Plugin.Name}]: {entry.Message}");
                break;
            case EngineStartedLogEntry entry:
                Console.WriteLine($"{prefix}Engine started at '{entry.RootDirectory}' with data directory '{entry.DataDirectory}'");
                break;
            case ScriptExecutionStartedLogEntry entry:
                Console.WriteLine($"{prefix}Running script '{entry.ScriptPath}'...");
                break;
            case ScriptExecutedSuccessfullyLogEntry entry:
                Console.WriteLine($"{prefix}Script executed successfully!");
                break;
            case ScriptExecutionFailedLogEntry entry:
                Console.WriteLine($"{prefix}Script execution failed!\nError: {entry.ErrorMessage}");
                if (entry.StackTrace != null) Console.WriteLine(entry.StackTrace);
                break;
            case ScriptLogEntry entry:
                Console.WriteLine($"{prefix}{Path.GetRelativePath(Directory.GetCurrentDirectory(), entry.ScriptPath)}: {entry.Message}");
                break;
        }
    }
}
