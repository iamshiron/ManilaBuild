
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Shiron.Manila.API;
using Shiron.Manila.Attributes;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;

namespace Shiron.Manila;

public sealed class ScriptContext(ManilaEngine engine, API.Component component, string scriptPath) {
    /// <summary>
    /// The script engine used by this context.
    /// </summary>
    public readonly ScriptEngine ScriptEngine = new V8ScriptEngine(
        V8ScriptEngineFlags.EnableTaskPromiseConversion
    ) {
        ExposeHostObjectStaticMembers = true
    };
    /// <summary>
    /// The engine this context is part of. Currently only used as an alias for the to not have to call GetInstance() all the time.
    /// </summary>
    public ManilaEngine Engine { get; private set; } = engine;
    /// <summary>
    /// The path to the script file.
    /// </summary>
    public string ScriptPath { get; private set; } = scriptPath;
    /// <summary>
    /// The component this context is part of.
    /// </summary>
    public readonly API.Component Component = component;
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
    /// Loads environment variables from a .env file if it exists in the project directory
    /// </summary>
    private void LoadEnvironmentVariables() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            // Clear any existing variables to ensure clean state
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
                foreach (string line in System.IO.File.ReadAllLines(envFilePath)) {
                    string trimmedLine = line.Trim();

                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith("//")) {
                        continue;
                    }

                    int equalIndex = trimmedLine.IndexOf('=');
                    if (equalIndex > 0) {
                        string key = trimmedLine.Substring(0, equalIndex).Trim();
                        string value = trimmedLine.Substring(equalIndex + 1).Trim();

                        // Remove quotes if they exist
                        if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                            (value.StartsWith("'") && value.EndsWith("'"))) {
                            value = value.Substring(1, value.Length - 2);
                        }

                        EnvironmentVariables[key] = value;
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
    public string GetEnvironmentVariable(string key) {
        if (EnvironmentVariables.TryGetValue(key, out string? value)) {
            return value;
        }
        return Environment.GetEnvironmentVariable(key) ?? string.Empty;
    }

    /// <summary>
    /// Sets a project-specific environment variable
    /// </summary>
    public void SetEnvironmentVariable(string key, string value) {
        EnvironmentVariables[key] = value;
    }

    /// <summary>
    /// Executes the script.
    /// </summary>
    public async System.Threading.Tasks.Task ExecuteAsync() {
        using (new ProfileScope(MethodBase.GetCurrentMethod()!)) {
            if (ManilaAPI == null) throw new ManilaException("ScriptEngine needs to be initialized before running a script!");

            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Logger.Log(new ScriptExecutionStartedLogEntry(ScriptPath, ContextID));
            try {
                var scriptContent = File.ReadAllTextAsync(ScriptPath);

                // Load environment variables before executing script
                LoadEnvironmentVariables();

                // Create a TaskCompletionSource to track script completion
                var taskCompletion = new TaskCompletionSource<bool>();

                ScriptEngine.AddHostObject("__Manila_signalCompletion", new Action(() => {
                    taskCompletion.TrySetResult(true);
                }));

                ScriptEngine.AddHostObject("__Manila_handleError", new Action<object>(e => {
                    Exception exceptionToThrow;

                    if (e == null) {
                        exceptionToThrow = new ScriptingException("Script error: null exception occurred");
                    } else if (e is Exception directException) {
                        // If it's already a .NET exception, wrap it in ScriptingException for consistency
                        exceptionToThrow = new ScriptingException($"Script execution failed: {directException.Message}", directException);
                    } else {
                        // Handle JavaScript errors and other objects
                        string errorMessage = "Unknown script error";

                        try {
                            // Try to extract meaningful error information from the JavaScript error object
                            if (e is ScriptObject scriptObj) {
                                var message = scriptObj.GetProperty("message");
                                var name = scriptObj.GetProperty("name");
                                var stack = scriptObj.GetProperty("stack");

                                if (message != null && message != Undefined.Value) {
                                    errorMessage = message.ToString() ?? "Unknown error";
                                }

                                if (name != null && name != Undefined.Value) {
                                    errorMessage = $"{name}: {errorMessage}";
                                }

                                // Include stack trace if available
                                if (stack != null && stack != Undefined.Value && !string.IsNullOrEmpty(stack.ToString())) {
                                    errorMessage += $"\n\nJavaScript Stack Trace:\n{stack}";
                                }
                            } else {
                                errorMessage = e.ToString() ?? "Unknown script error";
                            }
                        } catch {
                            // If we can't extract error info, use the toString representation
                            errorMessage = e.ToString() ?? "Unknown script error occurred";
                        }

                        exceptionToThrow = new ScriptingException($"JavaScript error: {errorMessage}");
                    }

                    taskCompletion.TrySetException(exceptionToThrow);
                }));

                ScriptEngine.AllowReflection = true;
                ScriptEngine.EnableAutoHostVariables = true;

                using (new ProfileScope("Executing Script")) {
                    ScriptEngine.Execute(new DocumentInfo(ScriptPath), $@"
                (async function() {{
                    try {{
                        {await scriptContent}
                        __Manila_signalCompletion();
                    }} catch (e) {{
                        __Manila_handleError(e);
                    }}
                }})();
            ");
                }

                // Wait for the script to either complete or throw an exception
                await taskCompletion.Task;

                Component.Finalize(ManilaAPI);
            } catch (ScriptEngineException see) {
                // Handle V8 script engine exceptions specifically
                var errorMessage = see.ErrorDetails ?? see.Message;
                var ex = new ScriptingException($"Script execution failed in '{Path.GetRelativePath(ManilaEngine.GetInstance().RootDir, ScriptPath)}': {errorMessage}", see);
                Logger.Log(new ScriptExecutionFailedLogEntry(ScriptPath, ex, ContextID));
                throw ex;
            } catch (ScriptingException) {
                // Re-throw ScriptingExceptions as-is (they're already properly formatted)
                throw;
            } catch (Exception e) {
                var ex = new ScriptingException($"An error occurred while executing script: '{Path.GetRelativePath(ManilaEngine.GetInstance().RootDir, ScriptPath)}'", e);
                Logger.Log(new ScriptExecutionFailedLogEntry(ScriptPath, ex, ContextID));
                throw ex;
            }
            Logger.Log(new ScriptExecutedSuccessfullyLogEntry(ScriptPath, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, ContextID));
        }
    }
    /// <summary>
    /// Executes the workspace script.
    /// </summary>
    public void ExecuteWorkspace() {
        try {
            // Load environment variables from workspace root
            LoadEnvironmentVariables();

            ScriptEngine.Execute(File.ReadAllText("Manila.js"));
        } catch (ScriptEngineException see) {
            var errorMessage = see.ErrorDetails ?? see.Message;
            Logger.Error($"Error in workspace script: {errorMessage}");
            throw new ScriptingException($"Workspace script execution failed: {errorMessage}", see);
        } catch (Exception e) {
            Logger.Error($"Error in workspace script: {e.Message}");
            throw new ScriptingException("Workspace script execution failed", e);
        }
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
