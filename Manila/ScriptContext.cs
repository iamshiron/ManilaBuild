
using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public sealed class ScriptContext(ManilaEngine engine, API.Component component, string scriptPath) {
    /// <summary>
    /// The script engine used by this context.
    /// </summary>
    [JsonIgnore]
    public readonly V8ScriptEngine ScriptEngine = new(
        V8ScriptEngineFlags.EnableTaskPromiseConversion
    ) {
        ExposeHostObjectStaticMembers = true
    };
    /// <summary>
    /// The engine this context is part of. Currently only used as an alias for the to not have to call GetInstance() all the time.
    /// </summary>

    [JsonIgnore]
    public ManilaEngine Engine { get; private set; } = engine;
    /// <summary>
    /// The path to the script file.
    /// </summary>
    public string ScriptPath { get; private set; } = scriptPath;
    /// <summary>
    /// The component this context is part of.
    /// </summary>
    public readonly Component Component = component;
    /// <summary>
    /// Mostly used for logging
    /// </summary>
    public readonly Guid ContextID = Guid.NewGuid();

    /// <summary>
    /// Project-specific environment variables that get isolated between projects
    /// </summary>
    private Dictionary<string, string> EnvironmentVariables { get; } = new();

    public API.Manila? ManilaAPI { get; private set; } = null;

    public List<Type> EnumComponents { get; } = new();

    public string GetCompiledFilePath() {
        var scriptDir = Path.GetDirectoryName(ScriptPath);
        var relativePath = Path.GetRelativePath(Engine.RootDir, scriptDir!);
        var fileName = $"{Path.GetFileNameWithoutExtension(relativePath)}.bin";
        return Path.Join(
            Engine.DataDir,
            "compiled",
            relativePath.Replace("/", "_").Replace("\\", "_"),
            fileName
        );
    }

    /// <summary>
    /// Initializes the script context.
    /// </summary>
    public void Init() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            Logger.Debug($"Initializing script context for '{ScriptPath}'");

            ManilaAPI = new API.Manila(this);
            ScriptEngine.AddHostObject("Manila", ManilaAPI);
            ScriptEngine.AddHostObject("print", (params object[] args) => {
                Logger.Log(new ScriptLogEntry(ScriptPath, string.Join(" ", args), ContextID));
            });

            foreach (var prop in Component.GetType().GetProperties()) {
                if (prop.GetCustomAttribute<ScriptProperty>() == null) continue;
                Component.AddScriptProperty(prop);
            }
            foreach (var func in Component.GetType().GetMethods()) {
                if (func.GetCustomAttribute<ScriptFunction>() == null) continue;
                Component.AddScriptFunction(func, ScriptEngine);
            }
        }
    }

    /// <summary>
    /// Asynchronously loads environment variables from a .env file if it exists.
    /// </summary>
    private async Task LoadEnvironmentVariables() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            EnvironmentVariables.Clear();

            string? projectDir = Path.GetDirectoryName(ScriptPath);
            if (projectDir == null) {
                Logger.Warning($"Could not determine project directory for '{ScriptPath}'.");
                return;
            }

            string envFilePath = Path.Combine(projectDir, ".env");

            if (!File.Exists(envFilePath)) {
                Logger.Debug($"No .env file found for '{ScriptPath}'.");
                return;
            }

            Logger.Debug($"Loading environment variables from '{envFilePath}'.");
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
                Logger.Warning($"Error loading environment variables: {ex.Message}");
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
    public async Task ExecuteAsync() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            if (ManilaAPI == null) {
                throw new ManilaException("ScriptEngine needs to be initialized before running a script!");
            }

            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Logger.Log(new ScriptExecutionStartedLogEntry(ScriptPath, ContextID));

            try {
                await LoadEnvironmentVariables();

                var jobCompletion = new TaskCompletionSource<bool>();
                SetupScriptEngine(jobCompletion);

                await ExecuteScript();

                // Await the script's completion signal.
                _ = await jobCompletion.Task;

                Component.Finalize(ManilaAPI);
            } catch (Exception e) {
                HandleExecutionException(e);
                // The above method re-throws, so this is the end of the line.
            }

            Logger.Log(new ScriptExecutedSuccessfullyLogEntry(ScriptPath, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, ContextID));
        }
    }

    /// <summary>
    /// Checks if the script file has changed, logs the result, and returns its content.
    /// </summary>
    /// <returns>The content of the script file.</returns>
    private bool CheckForScriptChanges(out string hash) {
        hash = HashUtils.HashFile(ScriptPath);
        return Engine.FileHashCache.HasChanged(ScriptPath, hash);
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
    private bool CompileScriptAndRead(string code, out byte[] bytes) {
        var documentInfo = new DocumentInfo(ScriptPath);
        var binaryFilePath = GetCompiledFilePath();
        var directory = Path.GetDirectoryName(binaryFilePath)!;

        var fileChanged = CheckForScriptChanges(out var fileHash);

        if (!fileChanged && File.Exists(binaryFilePath)) {
            Logger.Debug($"Using cached compiled script from '{binaryFilePath}'.");
            bytes = File.ReadAllBytes(binaryFilePath);
            return true;
        }

        Engine.FileHashCache.AddOrUpdate(ScriptPath, fileHash);
        if (!Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);

        _ = ScriptEngine.Compile(documentInfo, code, V8CacheKind.Code, out bytes);
        if (bytes != null && bytes.Length > 0) {
            File.WriteAllBytes(binaryFilePath, bytes);
            Logger.Debug($"Compiled script saved to '{binaryFilePath}'.");
            return false;
        } else {
            Logger.Error($"Failed to compile script '{ScriptPath}'. No bytecode generated.");
            return false;
        }
    }

    /// <summary>
    /// Wraps the script content in an async IIFE and executes it.
    /// </summary>
    /// <param name="scriptContent">The script code to execute.</param>
    private async Task ExecuteScript() {
        using (new ProfileScope("Executing Script")) {
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

            if (!CompileScriptAndRead(code, out var bytes)) {
                Logger.Warning($"Script '{ScriptPath}' could not be compiled or cached. Using raw code execution.");
            }

            var script = ScriptEngine.Compile(new DocumentInfo(ScriptPath), code, V8CacheKind.Code, bytes, out var accepted);
            if (!accepted) {
                Logger.Warning($"Script '{ScriptPath}' was not accepted by the script engine. It may have been modified or is invalid.");
            } else {
                Logger.Debug($"Script '{ScriptPath}' loaded successfully.");
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

        var relativePath = Path.GetRelativePath(ManilaEngine.GetInstance().RootDir, ScriptPath);
        var errorMessage = e is ScriptEngineException see ? see.ErrorDetails ?? see.Message : e.Message;
        var ex = new ScriptingException($"An error occurred in '{relativePath}': {errorMessage}", e);
        Logger.Log(new ScriptExecutionFailedLogEntry(ScriptPath, ex, ContextID));
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
            Logger.Warning($"Enum '{t}' already applied.");
            return;
        }

        EnumComponents.Add(t);
        ScriptEngine.AddHostType(t.Name[1..], t);
    }
}
