namespace Shiron.Manila;

using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

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

    public API.Manila? ManilaAPI { get; private set; } = null;

    public List<Type> EnumComponents { get; } = new();

    /// <summary>
    /// Initializes the script context.
    /// </summary>
    public void Init() {
        Logger.Debug($"Initializing script context for '{ScriptPath}'.");
        Logger.Debug("Adding Manila API to script engine.");

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
    /// Executes the script.
    /// </summary>
    public void Execute() {
        try {
            ScriptEngine.Execute(File.ReadAllText(ScriptPath));
        } catch (ScriptEngineException e) {
            Logger.Error("Error in script: " + ScriptPath);
            Logger.Info(e.Message);
            throw;
        }
    }
    /// <summary>
    /// Executes the workspace script.
    /// </summary>
    public void ExecuteWorkspace() {
        try {
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
        Logger.Debug($"Applying enum '{t}'.");

        if (t.GetType().GetCustomAttributes<ScriptEnum>() == null) throw new Exception($"Object '{t}' is not a script enum.");

        if (EnumComponents.Contains(t)) {
            Logger.Warn($"Enum '{t}' already applied.");
            return;
        }

        EnumComponents.Add(t);
        ScriptEngine.AddHostType(t.Name[1..], t);
    }
}
