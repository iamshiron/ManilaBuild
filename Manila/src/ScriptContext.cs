using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Attributes;
using Shiron.Manila.Caching;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public sealed class ScriptContext : IScriptContext {
    private readonly ILogger _logger;
    private readonly IProfiler _profiler;

    private readonly string _rootDir;
    private readonly string _dataDir;

    [JsonIgnore]
    public V8ScriptEngine ScriptEngine { get; }

    public string ScriptPath { get; private set; }
    public readonly string ScriptHash;

    public readonly Guid ContextID = Guid.NewGuid();
    private Dictionary<string, string> EnvironmentVariables { get; } = new();
    public List<Type> EnumComponents { get; } = [];
    public API.Manila? ManilaAPI { get; private set; } = null;

    public ScriptContext(ILogger logger, IProfiler profiler, V8ScriptEngine scriptEngine, string rootDir, string scriptPath) {
        _logger = logger;
        _profiler = profiler;
        ScriptEngine = scriptEngine;

        _rootDir = rootDir;
        _dataDir = Path.Combine(_rootDir, ".manila");
        ScriptPath = scriptPath;

        if (!File.Exists(scriptPath)) {
            throw new ConfigurationException($"The script file could not be found at the specified path: '{scriptPath}'.");
        }
        try {
            ScriptHash = HashUtils.HashFile(scriptPath);
        } catch (Exception ex) {
            throw new EnvironmentException($"Failed to read and hash the script file '{scriptPath}'. Check file permissions.", ex);
        }
    }

    public string GetCompiledFilePath() {
        var fileName = $"{ScriptHash[0..16]}.bin";
        return Path.Join(_dataDir, "compiled", fileName);
    }

    public void Init(API.Manila manilaAPI, ScriptBridge bridge, Component component) {
        ManilaAPI = manilaAPI;
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            _logger.Debug($"Initializing script context for '{ScriptPath}'");
            ScriptEngine.AddHostObject("Manila", ManilaAPI);
            ScriptEngine.AddHostObject("print", (params object[] args) => {
                _logger.Log(new ScriptLogEntry(ScriptPath, string.Join(" ", args), ContextID));
            });
        }
    }

    private async Task LoadEnvironmentVariablesAsync() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            EnvironmentVariables.Clear();

            string projectDir = Path.GetDirectoryName(ScriptPath) ?? throw new InternalLogicException($"Could not determine project directory for a valid script path '{ScriptPath}'.");
            string envFilePath = Path.Combine(projectDir, ".env");

            if (!File.Exists(envFilePath)) {
                _logger.Debug($"No .env file found for '{ScriptPath}'.");
                return;
            }

            _logger.Debug($"Loading environment variables from '{envFilePath}'.");
            try {
                foreach (string line in await File.ReadAllLinesAsync(envFilePath)) {
                    string trimmedLine = line.Trim();

                    if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith("//")) {
                        continue;
                    }

                    var split = trimmedLine.Split('=', 2);
                    if (split.Length == 2) {
                        EnvironmentVariables[split[0]] = split[1].Trim('"', '\'');
                    }
                }
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
                throw new EnvironmentException($"Error loading environment variables from '{envFilePath}'. Check file permissions and integrity.", ex);
            }
        }
    }

    public string? GetEnvironmentVariable(string key) {
        return EnvironmentVariables.TryGetValue(key, out string? value) ? value : null;
    }

    public void SetEnvironmentVariable(string key, string value) {
        EnvironmentVariables[key] = value;
    }

    public async Task ExecuteAsync(IFileHashCache cache, Component component) {
        // This is a programmer error; the caller is using the class incorrectly.
        if (ManilaAPI == null) {
            throw new InternalLogicException("ManilaAPI is not initialized. Call Init() before executing the script.");
        }

        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _logger.Log(new ScriptExecutionStartedLogEntry(ScriptPath, ContextID));

            try {
                await LoadEnvironmentVariablesAsync();

                var jobCompletion = new TaskCompletionSource<bool>();
                SetupScriptEngine(jobCompletion);

                await ExecuteScriptAsync(cache);
                _ = await jobCompletion.Task;

                component.Finalize(ManilaAPI);

            } catch (ManilaException) {
                throw;
            } catch (Exception ex) {
                throw new InternalLogicException($"An unexpected error occurred during script execution of '{ScriptPath}'. See inner exception for details.", ex);
            }

            _logger.Log(new ScriptExecutedSuccessfullyLogEntry(ScriptPath, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, ContextID));
        }
    }

    private bool CheckForScriptChanges(IFileHashCache cache) {
        return cache.HasChanged(ScriptPath, ScriptHash);
    }

    private void SetupScriptEngine(TaskCompletionSource<bool> jobCompletion) {
        ScriptEngine.AddHostObject("__Manila_signalCompletion", new Action(() => jobCompletion.TrySetResult(true)));
        ScriptEngine.AddHostObject("__Manila_handleError", new Action<object>(e => HandleJavaScriptError(e, jobCompletion)));
        ScriptEngine.AllowReflection = true;
        ScriptEngine.EnableAutoHostVariables = true;
    }

    private void HandleJavaScriptError(object e, TaskCompletionSource<bool> jobCompletion) {
        ScriptExecutionException exceptionToThrow;

        if (e is Exception directException) {
            exceptionToThrow = new ScriptExecutionException($"Script execution failed: {directException.Message}", ScriptPath, directException);
        } else {
            string errorMessage = e?.ToString() ?? "Unknown script error";
            string? name = null;
            string? stack = null;

            if (e is ScriptObject scriptObj) {
                var message = scriptObj.GetProperty("message")?.ToString() ?? "Unknown error";
                name = scriptObj.GetProperty("name")?.ToString();
                stack = scriptObj.GetProperty("stack")?.ToString();
                errorMessage = !string.IsNullOrEmpty(name) ? $"{name}: {message}" : message;
            }

            exceptionToThrow = new ScriptExecutionException(errorMessage, ScriptPath, name, stack);
        }

        jobCompletion.TrySetException(exceptionToThrow);
    }

    private async Task ExecuteScriptAsync(IFileHashCache cache) {
        using (new ProfileScope(_profiler, "Executing Script")) {
            string scriptContent;
            byte[]? cachedBytes = null;
            var binaryFilePath = GetCompiledFilePath();

            try {
                // Read the script file first.
                scriptContent = await File.ReadAllTextAsync(ScriptPath);

                var fileChanged = CheckForScriptChanges(cache);

                if (!fileChanged && File.Exists(binaryFilePath)) {
                    _logger.Debug($"Using cached compiled script from '{binaryFilePath}'.");
                    cachedBytes = await File.ReadAllBytesAsync(binaryFilePath);
                }
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
                throw new EnvironmentException($"Failed to read script file or its cache at '{ScriptPath}'. Check file permissions.", ex);
            }

            // This wrapper is essential for capturing async errors inside the JS code.
            var codeToExecute = $@"
                (async function() {{
                    try {{
                        {scriptContent}
                        __Manila_signalCompletion();
                    }} catch (e) {{
                        __Manila_handleError(e);
                    }}
                }})();";

            try {
                var documentInfo = new DocumentInfo(ScriptPath);
                var script = ScriptEngine.Compile(documentInfo, codeToExecute, V8CacheKind.Code, cachedBytes, out var cacheAccepted);

                if (!cacheAccepted && cachedBytes != null) {
                    _logger.Warning($"Cached script for '{ScriptPath}' was rejected. Recompiling.");
                    script = ScriptEngine.Compile(documentInfo, codeToExecute);
                }

                if (cacheAccepted == false) {
                    cache.AddOrUpdate(ScriptPath, ScriptHash);
                    // Overwrite the cache with the new compiled script.
                    script = ScriptEngine.Compile(documentInfo, codeToExecute, V8CacheKind.Code, out var newBytes);
                    var directory = Path.GetDirectoryName(binaryFilePath)!;
                    if (!Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);
                    await File.WriteAllBytesAsync(binaryFilePath, newBytes);
                    _logger.Debug($"Recompiled and saved new cache to '{binaryFilePath}'.");
                }

                ScriptEngine.Execute(script);

            } catch (ScriptEngineException ex) {
                throw new ScriptExecutionException($"Failed to compile or execute script: {ex.Message}", ScriptPath, ex);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                throw new EnvironmentException($"Failed to write script cache to '{binaryFilePath}'. Check directory permissions.", ex);
            }
        }
    }
}
