using System.Reflection;
using System.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using Shiron.Manila.API;
using Shiron.Manila.Ext;
using Shiron.Manila.Utils;
using System.Data.Common;
using System.Runtime.Intrinsics.Arm;
using System.Text.RegularExpressions;

namespace Shiron.Manila.Logging;

public interface ILogEntry {
    public abstract long Timestamp { get; }
    public abstract LogLevel Level { get; }
}

public abstract class BaseLogEntry : ILogEntry {
    public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public abstract LogLevel Level { get; }
    public Guid? ParentContextID { get; } = LogContext.CurrentContextId;
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
public class ExceptionConverter : JsonConverter {
    public override bool CanConvert(Type objectType) {
        return typeof(Exception).IsAssignableFrom(objectType);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
        var exception = (Exception) value;
        var jo = new JObject {
            { "message", exception.Message },
            { "stackTrace", exception.StackTrace },
            { "hResult", exception.HResult },
            { "source", exception.Source }
        };

        if (exception.InnerException != null) {
            jo.Add("innerException", JToken.FromObject(exception.InnerException, serializer));
        }

        jo.WriteTo(writer);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
        // Deserialization is not implemented for this example.
        throw new NotSupportedException("Deserializing exceptions is not supported.");
    }
}

// --- Data Classes for Log Events --- //
public sealed class PluginInfo(ManilaPlugin plugin) {
    public readonly string Name = plugin.Name;
    public readonly string Group = plugin.Group;
    public readonly string Version = plugin.Version;
    public readonly string[] Authors = plugin.Authors.ToArray();
    public readonly string Entry = plugin.GetType().FullName;
    public readonly string[] NuGetDependencies = plugin.NugetDependencies.ToArray();
    public readonly string File = plugin.File;
}
public sealed class TaskInfo(API.Task task) {
    public readonly string Name = task.Name;
    public readonly string ID = task.GetIdentifier();
    public readonly string ScriptPath = task.ScriptPath;
    public readonly string Description = task.Description;
    public readonly ComponentInfo Component = new(task.Component);
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
public sealed class ExecutableObjectInfo(ExecutableObject obj) {
    public readonly string ID = obj.GetID();
    public readonly string Type = obj.GetType().FullName ?? "Unknown";
    public readonly bool Blocking = obj.IsBlocking();
}
public sealed class ExecutionLayerInfo(ExecutionGraph.ExecutionLayer layer) {
    public readonly ExecutableObjectInfo[] Items = layer.Items.Select(obj => new ExecutableObjectInfo(obj)).ToArray();
}

public sealed class ExceptionInfo(Exception e) {
    public readonly string Type = e.GetType().FullName ?? "Unknown Exception";
    public readonly string Message = e.Message;
    public readonly string StackTrace = e.StackTrace ?? "Empty Stack Trace";
    public readonly List<ExceptionInfo> CausedBy = GetCausedBy(e);

    private static List<ExceptionInfo> GetCausedBy(Exception ex) {
        var list = new List<ExceptionInfo>();
        var inner = ex.InnerException;
        while (inner != null) {
            list.Add(new ExceptionInfo(inner));
            inner = inner.InnerException;
        }
        return list;
    }
}

// --- Misc Logging Events --- //

public class BasicLogEntry(string message, LogLevel level) : BaseLogEntry {
    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
}

public class BasicPluginLogEntry(ManilaPlugin plugin, string message, LogLevel level) : BaseLogEntry {
    public override LogLevel Level { get; } = level;
    public string Message { get; } = message;
    public PluginInfo Plugin { get; } = new(plugin);
}

// --- Lifecycle Logging Events --- //
public class EngineStartedLogEntry(string rootDir, string dataDir) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string RootDirectory { get; } = rootDir;
    public string DataDirectory { get; } = dataDir;
}

public class BuildLayersLogEntry(ExecutionGraph.ExecutionLayer[] layers) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public ExecutionLayerInfo[] Layers { get; } = layers.Select(layer => new ExecutionLayerInfo(layer)).ToArray();
}
public class BuildLayerStartedLogEntry(ExecutionGraph.ExecutionLayer layer, Guid contextID, int layerIndex) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public ExecutionLayerInfo Layer { get; } = new(layer);
    public int LayerIndex { get; } = layerIndex;
    public string ContextID { get; } = contextID.ToString();
}
public class BuildLayerCompletedLogEntry(ExecutionGraph.ExecutionLayer layer, Guid contextID, int layerIndex) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public ExecutionLayerInfo Layer { get; } = new(layer);
    public int LayerIndex { get; } = layerIndex;
    public string ContextID { get; } = contextID.ToString();
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
    public Exception Exception { get; } = e;
}

public class ProjectsInitializedLogEntry(long duration) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
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

public class ScriptExecutionFailedLogEntry(string scriptPath, Exception exception, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ScriptPath { get; } = scriptPath;
    public Exception Exception { get; } = exception;
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

public class TaskExecutionFailedLogEntry(API.Task task, Guid contextID, Exception exception) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public TaskInfo Task { get; } = new(task);
    public string ContextID { get; } = contextID.ToString();
    public Exception Exception { get; } = exception;
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
    public ComponentInfo Component { get; } = new(component);
    public TaskInfo Task { get; } = new(task);
}

// --- Misc Log Entries --- //
public class CommandExecutionLogEntry(Guid contextID, string executable, string[] args, string workingDir) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string ContextID { get; } = contextID.ToString();
    public string Executable { get; } = executable;
    public string[] Args { get; } = args;
    public string WorkingDir { get; } = workingDir;
}

public class CommandExecutionFinishedLogEntry(Guid contextID, string stdOut, string stdErr, long duration, int exitCode) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string ContextID { get; } = contextID.ToString();
    public string StdOut { get; } = stdOut;
    public string StdErr { get; } = stdErr;
    public long Duration { get; } = duration;
    public int ExitCode { get; } = exitCode;
}

public class CommandExecutionFailedLogEntry(Guid contextID, string stdOut, string stdErr, long duration, int exitCode) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ContextID { get; } = contextID.ToString();
    public string StdOut { get; } = stdOut;
    public string StdErr { get; } = stdErr;
    public long Duration { get; } = duration;
    public int ExitCode { get; } = exitCode;
}

public class CommandStdOutLogEntry(Guid contextID, string message, bool quiet) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Info;
    public string ContextID { get; } = contextID.ToString();
    public string Message { get; } = message;
    public bool Quiet { get; } = quiet;
}
public class CommandStdErrLogEntry(Guid contextID, string message, bool quiet) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Error;
    public string ContextID { get; } = contextID.ToString();
    public string Message { get; } = message;
    public bool Quiet { get; } = quiet;
}

// --- Plugin Loading Entries --- //
public class NuGetPackageLoadingLogEntry(string id, string version, ManilaPlugin plugin, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public string PackageID { get; } = id;
    public string PackageVersion { get; } = version;
    public PluginInfo Plugin { get; } = new(plugin);
    public string ContextID { get; } = contextID.ToString();
}
public partial class NuGetSubPackageLoadingEntry(string assembly, Guid contextID) : BaseLogEntry {
    public static readonly Regex assemblyRegex = AssemblyRegex();

    public override LogLevel Level => LogLevel.Debug;
    public string PackageID { get; } = GetPackageID(assembly);
    public string PackageVersion { get; } = GetPackageVersion(assembly);
    public string ContextID { get; } = contextID.ToString();

    private static string GetPackageID(string assembly) {
        var match = assemblyRegex.Match(assembly);
        return match.Success ? match.Groups["package"].Value : assembly;
    }

    private static string GetPackageVersion(string assembly) {
        var match = assemblyRegex.Match(assembly);
        return match.Success ? match.Groups["version"].Value : assembly;
    }

    [GeneratedRegex(@"(?<package>[\w\.]+?)_(?<version>[\d\.]+?)[\\\/]")]
    private static partial Regex AssemblyRegex();
}
public class LoadingPluginLogEntry(ManilaPlugin plugin, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.Debug;
    public PluginInfo Plugin { get; } = new(plugin);
    public string ContextID { get; } = contextID.ToString();
}
public class LoadingPluginsLogEntry(string pluginPath, Guid contextID) : BaseLogEntry {
    public override LogLevel Level => LogLevel.System;
    public string PluginPath { get; } = pluginPath;
    public string ContextID { get; } = contextID.ToString();
}
