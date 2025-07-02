using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Shiron.Manila.API;
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

// --- Data Classes for Log Events --- //
public sealed class PluginInfo(string name, string group, string version) {
    public readonly string Name = name;
    public readonly string Group = group;
    public readonly string Version = version;
}
public sealed class TaskInfo(API.Task task) {
    public readonly string Name = task.Name;
    public readonly string ScriptPath = task.ScriptPath;
    public readonly string Description = task.Description;
}
public sealed class ComponentInfo(Component component) {
    public readonly bool IsProject = component is Project;
    public readonly bool IsWorkspace = component is Workspace;
    public readonly string Root = component.Path.get();
    public readonly string ID = component.GetIdentifier();
}
public sealed class ProjectInfo(Project project) {
    public readonly string Name = project.Name;
    public readonly string Identifier = project.GetIdentifier();
    public readonly string? Version = project.Version;
    public readonly string? Group = project.Group;
    public readonly string? Description = project.Description;
    public readonly string Root = project.Path.get();
}
// --- Misc Logging Events --- //

public class BasicLogEntry(string message, LogLevel level) : BaseLogEntry {
    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
}

public class BasicPluginLogEntry(ManilaPlugin plugin, string message, LogLevel level) : BaseLogEntry {
    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
    public PluginInfo Plugin { get; } = new(plugin.Name, plugin.Version, plugin.Group);
}

// --- Lifecycle Logging Events --- //
public class EngineStartedLogEntry(string rootDir, string dataDir) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string RootDirectory { get; } = rootDir;
    public string DataDirectory { get; } = dataDir;
}

public class BuildStartedLogEntry : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
}

public class BuildCompletedLogEntry(long duration) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public long Duration { get; } = duration;
}

public class BuildFailedLogEntry(long duration, Exception e) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public long Duration { get; } = duration;
}

// --- Script Logging Events --- //
public class ScriptExecutionStartedLogEntry(string scriptPath, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
    public string ContextID { get; } = contextID.ToString();
}

public class ScriptLogEntry(string scriptPath, string message, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
    public string Message { get; } = message;
    public string ContextID { get; } = contextID.ToString();
}

public class ScriptExecutedSuccessfullyLogEntry(string scriptPath, long executionTimeMS, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ScriptPath { get; } = scriptPath;
    public long ExecutionTimeMS { get; } = executionTimeMS;
    public string ContextID { get; } = contextID.ToString();
}

public class ScriptExecutionFailedLogEntry(string scriptPath, string errorMessage, Guid contextID, string? stackTrace = null) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ScriptPath { get; } = scriptPath;
    public string ErrorMessage { get; } = errorMessage;
    public string? StackTrace { get; } = stackTrace;
    public string ContextID { get; } = contextID.ToString();
}

public class TaskExecutionStartedLogEntry(API.Task task, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public TaskInfo Task { get; } = new(task);
    public string ContextID { get; } = contextID.ToString();
}

public class TaskExecutionFinishedLogEntry(API.Task task, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public TaskInfo Task { get; } = new(task);
    public string ContextID { get; } = contextID.ToString();
}

// --- Misc Log Entries --- //
public class CommandExecutionLogEntry(Guid contextID, string executable, string[] args, string workingDir) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string ContextID { get; } = contextID.ToString();
    public string Executable { get; } = executable;
    public string[] Args { get; } = args;
    public string WorkingDir { get; } = workingDir;
}

public class CommandExecutionFinishedLogEntry(Guid contextID, string stdOut, string stdErr, long duration) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string ContextID { get; } = contextID.ToString();
    public string StdOut { get; } = stdOut;
    public string StdErr { get; } = stdErr;
    public long Duration { get; } = duration;
}

public class CommandExecutionFailedLogEntry(Guid contextID, string stdOut, string stdErr, long duration) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ContextID { get; } = contextID.ToString();
    public string StdOut { get; } = stdOut;
    public string StdErr { get; } = stdErr;
    public long Duration { get; } = duration;
}

// -- Discovery Logs -- //
public class ProjectDiscoveredLogEntry(string root, string script) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string Root { get; } = root;
    public string Script { get; } = script;
}
public class ProjectInitializedLogEntry(Project project) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ProjectInfo Projet { get; } = new(project);
}

public class TaskDiscoveredLogEntry(API.Task task, Component component) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ComponentInfo Component = new(component);
    public TaskInfo Task { get; } = new(task);
}
