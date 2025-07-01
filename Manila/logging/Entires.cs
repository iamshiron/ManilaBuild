using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.Ext;

namespace Shiron.Manila.Logging;

public interface ILogEntry {
    long Timestamp { get; }
    LogLevel Level { get; }
}

public abstract class BaseLogEntry : ILogEntry {
    public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public abstract LogLevel Level { get; }
}

public class LogEntryConverter : JsonConverter<ILogEntry> {
    public override bool CanWrite => true;
    public override bool CanRead => false;

    public override ILogEntry ReadJson(JsonReader reader, Type objectType, ILogEntry? existingValue, bool hasExistingValue, JsonSerializer serializer) {
        throw new NotImplementedException("Deserialization of ILogEntry is not supported by this converter.");
    }

    public override void WriteJson(JsonWriter writer, ILogEntry? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        // Serialize the main properties of ILogEntry directly
        var nestedSerializer = new JsonSerializer {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            TypeNameHandling = TypeNameHandling.None
        };

        // Explicitly remove this converter from the nested serializer to prevent stack overflow
        if (nestedSerializer.Converters.Any(c => c.GetType() == typeof(LogEntryConverter))) {
            nestedSerializer.Converters.Remove(nestedSerializer.Converters.First(c => c.GetType() == typeof(LogEntryConverter)));
        }
        // Inherit settings from parent serializer
        nestedSerializer.ReferenceLoopHandling = serializer.ReferenceLoopHandling;
        nestedSerializer.PreserveReferencesHandling = serializer.PreserveReferencesHandling;

        // Cast ContractResolver to DefaultContractResolver to access NamingStrategy
        var contractResolver = nestedSerializer.ContractResolver as DefaultContractResolver;
        var namingStrategy = contractResolver?.NamingStrategy;

        string GetFormattedPropertyName(string name) {
            return namingStrategy?.GetPropertyName(name, false) ?? name;
        }

        writer.WritePropertyName("type");
        writer.WriteValue(value.GetType().FullName);

        writer.WritePropertyName(GetFormattedPropertyName(nameof(ILogEntry.Timestamp)));
        writer.WriteValue(value.Timestamp);

        writer.WritePropertyName(GetFormattedPropertyName(nameof(ILogEntry.Level)));
        writer.WriteValue(value.Level.ToString());

        // Put all custom properties into a nested "data" object
        writer.WritePropertyName(GetFormattedPropertyName("data"));
        writer.WriteStartObject();

        Type type = value.GetType();
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (property.Name == nameof(ILogEntry.Timestamp) ||
                property.Name == nameof(ILogEntry.Level)) {
                continue;
            }

            object? propertyValue = property.GetValue(value);

            writer.WritePropertyName(GetFormattedPropertyName(property.Name));
            nestedSerializer.Serialize(writer, propertyValue);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}

// --- Misc Logging Events ---

public class BasicLogEntry(string message, LogLevel level) : BaseLogEntry {
    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
}

public class BasicPluginLogEntry(ManilaPlugin plugin, string message, LogLevel level) : BaseLogEntry {
    public sealed class PluginInfo(string name, string group, string version) {
        public readonly string Name = name;
        public readonly string Group = group;
        public readonly string Version = version;
    }

    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
    public PluginInfo Plugin { get; } = new PluginInfo(plugin.Name, plugin.Version, plugin.Group);
}

// --- Lifecycle Logging Events ---

public class EngineStartedLogEntry(string rootDir, string dataDir) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string RootDirectory { get; } = rootDir;
    public string DataDirectory { get; } = dataDir;
}

// --- Script Logging Events ---
public class ScriptExecutionStartedLogEntry(string scriptPath) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
}

public class ScriptLogEntry(string scriptPath, string message) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
    public string Message { get; } = message;
}

public class ScriptExecutedSuccessfullyLogEntry(string scriptPath, long executionTimeMS) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
    public long ExecutionTimeMS { get; } = executionTimeMS;
}

public class ScriptExecutionFailedLogEntry(string scriptPath, string errorMessage, string? stackTrace = null) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ScriptPath { get; } = scriptPath;
    public string ErrorMessage { get; } = errorMessage;
    public string? StackTrace { get; } = stackTrace;
}
