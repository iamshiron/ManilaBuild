
using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.Attributes;
using Shiron.Manila.Caching;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public interface IScriptContext {
    /// <summary>
    /// A list of enum types that have been applied to the script engine.
    /// </summary>
    List<Type> EnumComponents { get; }

    V8ScriptEngine ScriptEngine { get; }

    /// <summary>
    /// Gets the full path for the compiled script file.
    /// </summary>
    /// <returns>The path to the compiled file.</returns>
    string GetCompiledFilePath();

    /// <summary>
    /// Initializes the script context, setting up the script engine and APIs.
    /// </summary>
    void Init(API.Manila manilaAPI, ScriptBridge bridge, Component component);

    /// <summary>
    /// Gets an environment variable, checking project-specific variables first.
    /// </summary>
    /// <param name="key">The key of the environment variable.</param>
    /// <returns>The value of the environment variable, or null if not found.</returns>
    string? GetEnvironmentVariable(string key);

    /// <summary>
    /// Sets a project-specific environment variable.
    /// </summary>
    /// <param name="key">The key of the environment variable.</param>
    /// <param name="value">The value to set.</param>
    void SetEnvironmentVariable(string key, string value);

    /// <summary>
    /// Asynchronously executes the script associated with this context.
    /// </summary>
    /// <returns>A task that represents the asynchronous execution operation.</returns>
    Task ExecuteAsync(FileHashCache cache, Component component);

    /// <summary>
    /// Applies an enum type to the script engine, making it available in the script.
    /// </summary>
    /// <typeparam name="T">The enum type to apply.</typeparam>
    void ApplyEnum<T>();

    /// <summary>
    /// Applies an enum type to the script engine, making it available in the script.
    /// </summary>
    /// <param name="t">The enum type to apply.</param>
    void ApplyEnum(Type t);
}

public sealed class ScriptContext(ILogger logger, IProfiler profiler, V8ScriptEngine scriptEngine, string rootDir, string scriptPath) : IScriptContext {
    private readonly ILogger _logger = logger;
    private readonly IProfiler _profiler = profiler;

    private readonly string _rootDir = rootDir;
    private readonly string _dataDir = Path.Join(rootDir, ".manila");

    /// <summary>
    /// The script engine used by this context.
    /// </summary>
    [JsonIgnore]
    public V8ScriptEngine ScriptEngine { get => scriptEngine; }

    /// <summary>
    /// The path to the script file.
    /// </summary>
    public string ScriptPath { get; private set; } = scriptPath;
    public readonly string ScriptHash = HashUtils.HashFile(scriptPath);

    /// <summary>
    /// Mostly used for logging
    /// </summary>
    public readonly Guid ContextID = Guid.NewGuid();

    /// <summary>
    /// Project-specific environment variables that get isolated between projects
    /// </summary>
    private Dictionary<string, string> EnvironmentVariables { get; } = new();

    public List<Type> EnumComponents { get; } = [];

    public API.Manila? ManilaAPI { get; private set; } = null;

    public string GetCompiledFilePath() {
        var fileName = $"{ScriptHash[0..16]}.bin";
        return Path.Join(
            _dataDir,
            "compiled",
            fileName
        );
    }

    /// <summary>
    /// Initializes the script context.
    /// </summary>
    public void Init(API.Manila manilaAPI, ScriptBridge bridge, Component component) {
        ManilaAPI = manilaAPI;

        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            _logger.Debug($"Initializing script context for '{ScriptPath}'");

            ScriptEngine.AddHostObject("Manila", ManilaAPI);
            ScriptEngine.AddHostObject("print", (params object[] args) => {
                _logger.Log(new ScriptLogEntry(ScriptPath, string.Join(" ", args), ContextID));
            });

            foreach (var prop in ScriptEngine.GetType().GetProperties()) {
                if (prop.GetCustomAttribute<ScriptProperty>() == null) continue;
                ScriptBridgeContextApplyer.AddScriptProperty(_logger, this, bridge, prop);
            }
            foreach (var func in component.GetType().GetMethods()) {
                if (func.GetCustomAttribute<ScriptFunction>() == null) continue;
                ScriptBridgeContextApplyer.AddScriptFunction(_logger, bridge, func, ScriptEngine);
            }
        }
    }

    /// <summary>
    /// Asynchronously loads environment variables from a .env file if it exists.
    /// </summary>
    private async Task LoadEnvironmentVariables() {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            EnvironmentVariables.Clear();

            string? projectDir = Path.GetDirectoryName(ScriptPath);
            if (projectDir == null) {
                _logger.Warning($"Could not determine project directory for '{ScriptPath}'.");
                return;
            }

            string envFilePath = Path.Combine(projectDir, ".env");

            if (!File.Exists(envFilePath)) {
                _logger.Debug($"No .env file found for '{ScriptPath}'.");
                return;
            }

            _logger.Debug($"Loading environment variables from '{envFilePath}'.");
            try {
                // 3. Use ReadAllLinesAsync for non-blocking file I/O.
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
            } catch (Exception ex) {
                _logger.Warning($"Error loading environment variables: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets an environment variable, first checking project-specific variables, then system variables
    /// </summary>
    public string? GetEnvironmentVariable(string key) {
        return EnvironmentVariables.TryGetValue(key, out string? value) ? value : null;
    }

    /// <summary>
    /// Sets a project-specific environment variable
    /// </summary>
    public void SetEnvironmentVariable(string key, string value) {
        EnvironmentVariables[key] = value;
    }

    /// <summary>
    /// Executes the script after performing necessary checks and setup.
    /// </summary>
    public async Task ExecuteAsync(FileHashCache cache, Component component) {
        if (ManilaAPI == null) throw new ManilaException("ManilaAPI is not initialized. Call Init() before executing the script.");

        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _logger.Log(new ScriptExecutionStartedLogEntry(ScriptPath, ContextID));

            try {
                await LoadEnvironmentVariables();

                var jobCompletion = new TaskCompletionSource<bool>();
                SetupScriptEngine(jobCompletion);

                await ExecuteScript(cache);

                // Await the script's completion signal.
                _ = await jobCompletion.Task;

                component.Finalize(ManilaAPI);
            } catch (Exception e) {
                HandleExecutionException(e);
                // The above method re-throws, so this is the end of the line.
            }

            _logger.Log(new ScriptExecutedSuccessfullyLogEntry(ScriptPath, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, ContextID));
        }
    }

    /// <summary>
    /// Checks if the script file has changed, logs the result, and returns its content.
    /// </summary>
    /// <returns>The content of the script file.</returns>
    private bool CheckForScriptChanges(FileHashCache cache) {
        return cache.HasChanged(ScriptPath, ScriptHash);
    }

    /// <summary>
    /// Configures the script engine with host objects for completion and error handling.
    /// </summary>
    /// <param name="jobCompletion">The TaskCompletionSource to signal script status.</param>
    private void SetupScriptEngine(TaskCompletionSource<bool> jobCompletion) {
        ScriptEngine.AddHostObject("__Manila_signalCompletion", new Action(() => jobCompletion.TrySetResult(true)));
        ScriptEngine.AddHostObject("__Manila_handleError", new Action<object>(e => HandleJavaScriptError(e, jobCompletion)));
        ScriptEngine.AllowReflection = true;
        ScriptEngine.EnableAutoHostVariables = true;
    }

    /// <summary>
    /// Handles errors passed from the JavaScript environment, converting them to .NET exceptions.
    /// </summary>
    /// <param name="e">The error object from JavaScript.</param>
    /// <param name="jobCompletion">The TaskCompletionSource to set the exception on.</param>
    private static void HandleJavaScriptError(object e, TaskCompletionSource<bool> jobCompletion) {
        Exception exceptionToThrow;

        if (e == null) {
            exceptionToThrow = new ScriptingException("Script error: null exception occurred");
        } else if (e is Exception directException) {
            // Wrap existing .NET exception for consistency.
            exceptionToThrow = new ScriptingException($"Script execution failed: {directException.Message}", directException);
        } else {
            // Attempt to build a detailed error from the JavaScript error object.
            string errorMessage = e.ToString() ?? "Unknown script error";
            if (e is ScriptObject scriptObj) {
                var message = scriptObj.GetProperty("message")?.ToString() ?? "Unknown error";
                var name = scriptObj.GetProperty("name")?.ToString();
                var stack = scriptObj.GetProperty("stack")?.ToString();

                errorMessage = !string.IsNullOrEmpty(name) ? $"{name}: {message}" : message;
                if (!string.IsNullOrEmpty(stack)) {
                    errorMessage += $"\n\nJavaScript Stack Trace:\n{stack}";
                }
            }
            exceptionToThrow = new ScriptingException($"JavaScript error: {errorMessage}");
        }

        _ = jobCompletion.TrySetException(exceptionToThrow);
    }

    /// <summary>
    /// Compiles the script code and reads the resulting bytecode.
    /// If the script has not changed since the last compilation, it uses the cached bytecode
    /// </summary>
    /// <param name="code">The script code to compile.</param>
    /// <param name="bytes">The compiled bytecode output.</param>
    /// <returns>True if compilation was successful or cached bytecode was used, false otherwise.</returns>
    private bool CompileScriptAndRead(FileHashCache cahce, string code, out byte[] bytes) {
        var documentInfo = new DocumentInfo(ScriptPath);
        var binaryFilePath = GetCompiledFilePath();
        var directory = Path.GetDirectoryName(binaryFilePath)!;

        var fileChanged = CheckForScriptChanges(cahce);

        if (!fileChanged && File.Exists(binaryFilePath)) {
            _logger.Debug($"Using cached compiled script from '{binaryFilePath}'.");
            bytes = File.ReadAllBytes(binaryFilePath);
            return true;
        }

        cahce.AddOrUpdate(ScriptPath, ScriptHash);
        if (!Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);

        _ = ScriptEngine.Compile(documentInfo, code, V8CacheKind.Code, out bytes);
        if (bytes != null && bytes.Length > 0) {
            File.WriteAllBytes(binaryFilePath, bytes);
            _logger.Debug($"Compiled script saved to '{binaryFilePath}'.");
            return false;
        } else {
            _logger.Error($"Failed to compile script '{ScriptPath}'. No bytecode generated.");
            return false;
        }
    }

    /// <summary>
    /// Wraps the script content in an async IIFE and executes it.
    /// </summary>
    /// <param name="scriptContent">The script code to execute.</param>
    private async Task ExecuteScript(FileHashCache cache) {
        using (new ProfileScope(_profiler, "Executing Script")) {
            var scriptContent = await File.ReadAllTextAsync(ScriptPath);
            var code = $@"
                (async function() {{
                    try {{
                        {scriptContent}
                        __Manila_signalCompletion();
                    }} catch (e) {{
                        __Manila_handleError(e);
                    }}
                }})();";

            if (!CompileScriptAndRead(cache, code, out var bytes)) {
                _logger.Warning($"Script '{ScriptPath}' could not be compiled or cached. Using raw code execution.");
            }

            var script = ScriptEngine.Compile(new DocumentInfo(ScriptPath), code, V8CacheKind.Code, bytes, out var accepted);
            if (!accepted) {
                _logger.Warning($"Script '{ScriptPath}' was not accepted by the script engine. It may have been modified or is invalid.");
            } else {
                _logger.Debug($"Script '{ScriptPath}' loaded successfully.");
            }

            ScriptEngine.Execute(script);
        }
    }

    /// <summary>
    /// Logs script execution failures and re-throws a formatted exception.
    /// </summary>
    /// <param name="e">The exception caught during execution.</param>
    private void HandleExecutionException(Exception e) {
        // Re-throw ScriptingExceptions as they are already formatted.
        if (e is ScriptingException) throw e;

        var relativePath = Path.GetRelativePath(_rootDir, ScriptPath);
        var errorMessage = e is ScriptEngineException see ? see.ErrorDetails ?? see.Message : e.Message;
        var ex = new ScriptingException($"An error occurred in '{relativePath}': {errorMessage}", e);
        _logger.Log(new ScriptExecutionFailedLogEntry(ScriptPath, ex, ContextID));
        throw ex;
    }

    /// <summary>
    /// Applies an enum to the script engine.
    /// </summary>
    /// <typeparam name="T">The enum type</typeparam>
    public void ApplyEnum<T>() {
        ApplyEnum(typeof(T));
    }
    /// <summary>
    /// Applies an enum to the script engine.
    /// </summary>
    /// <typeparam name="T">The enum type</typeparam>
    /// <exception cref="Exception">The class is not tagged with the <see cref="ScriptEnum"/> attribute.</exception>
    public void ApplyEnum(Type t) {
        if (t.GetType().GetCustomAttributes<ScriptEnum>() == null) throw new Exception($"Object '{t}' is not a script enum.");

        if (EnumComponents.Contains(t)) {
            _logger.Warning($"Enum '{t}' already applied.");
            return;
        }

        EnumComponents.Add(t);
        ScriptEngine.AddHostType(t.Name[1..], t);
    }
}
