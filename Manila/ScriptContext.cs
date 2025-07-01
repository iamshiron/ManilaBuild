namespace Shiron.Manila;

using System.Dynamic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Shiron.Manila.Attributes;
using Shiron.Manila.Logging;

public sealed class ScriptContext(ManilaEngine engine, API.Component component, string scriptPath) {
    /// <summary>
    /// The script engine used by this context.
    /// </summary>
    public readonly ScriptEngine ScriptEngine = new V8ScriptEngine();
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
    /// Project-specific environment variables that get isolated between projects
    /// </summary>
    private Dictionary<string, string> EnvironmentVariables { get; } = new();

    public API.Manila? ManilaAPI { get; private set; } = null;

    public List<Type> EnumComponents { get; } = new();

    /// <summary>
    /// Initializes the script context.
    /// </summary>
    public void Init() {
        Logger.Debug($"Initializing script context for '{ScriptPath}'");

        ManilaAPI = new API.Manila(this);
        ScriptEngine.AddHostObject("Manila", ManilaAPI);
        ScriptEngine.AddHostObject("print", (params object[] args) => {
            ApplicationLogger.ScriptLog(args);
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

    /// <summary>
    /// Loads environment variables from a .env file if it exists in the project directory
    /// </summary>
    private void LoadEnvironmentVariables() {
        // Clear any existing variables to ensure clean state
        EnvironmentVariables.Clear();

        string? projectDir = System.IO.Path.GetDirectoryName(ScriptPath);
        if (projectDir == null) {
            Logger.Warn($"Could not determine project directory for '{ScriptPath}'.");
            return;
        }

        string envFilePath = System.IO.Path.Combine(projectDir, ".env");

        if (!System.IO.File.Exists(envFilePath)) {
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
            Logger.Warn($"Error loading environment variables: {ex.Message}");
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
    }    /// <summary>
         /// Executes the script.
         /// </summary>
    public void Execute() {
        Logger.Info($"Executing script '{ScriptPath}'.");
        try {
            // Load environment variables before executing script
            LoadEnvironmentVariables();

            // Create a TaskCompletionSource to track script completion
            var taskCompletion = new TaskCompletionSource<bool>();

            ScriptEngine.AddHostObject("__Manila_signalCompletion", new Action(() => {
                taskCompletion.TrySetResult(true);
            }));

            ScriptEngine.AddHostObject("__Manila_handleError", new Action<object>(e => {
                if (e == null) {
                    taskCompletion.TrySetException(new Exception("Script error: null exception"));
                    return;
                }
                if (e is not Exception) {
                    taskCompletion.TrySetException(new Exception("Script error: " + e.ToString()));
                    return;
                }
                taskCompletion.TrySetException(e as Exception);
            }));

            ScriptEngine.AllowReflection = true;
            ScriptEngine.EnableAutoHostVariables = true;

            // Execute the script with proper error handling
            ScriptEngine.Execute($@"
                (async function() {{
                    try {{
                        {File.ReadAllText(ScriptPath)}
                        __Manila_signalCompletion();
                    }} catch (e) {{
                        __Manila_handleError(e);
                    }}
                }})();
            ");

            // Wait for the script to either complete or throw an exception
            taskCompletion.Task.Wait();
        } catch (Exception e) {
            Logger.Error("Error in script: " + ScriptPath);
            Logger.Info(e.Message);
            throw;
        }
        Logger.Info($"Script '{ScriptPath}' executed successfully.");
    }
    /// <summary>
    /// Executes the workspace script.
    /// </summary>
    public void ExecuteWorkspace() {
        try {
            // Load environment variables from workspace root
            LoadEnvironmentVariables();

            ScriptEngine.Execute(File.ReadAllText("Manila.js"));
        } catch (ScriptEngineException e) {
            Logger.Error("Error in workspace script!");
            Logger.Info(e.Message);
            throw;
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
            Logger.Warn($"Enum '{t}' already applied.");
            return;
        }

        EnumComponents.Add(t);
        ScriptEngine.AddHostType(t.Name[1..], t);
    }
}
