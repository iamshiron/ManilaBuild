using System.Reflection;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Utils;

namespace Shiron.Manila;

public interface IScriptEntry {
    Task ExecuteAsync(API.Manila Manila);
}

public sealed class ScriptContext : IScriptContext {
    private readonly ILogger _logger;
    private readonly IProfiler _profiler;
    private readonly IExtensionManager _extensionManager;

    private readonly string _rootDir;
    private readonly string _dataDir;

    [JsonIgnore]
    private readonly Task<V8ScriptEngine> _scriptEngine;

    public string ScriptPath { get; private set; }
    public readonly string ScriptHash;

    public Guid ContextID { get; } = Guid.NewGuid();
    private Dictionary<string, string> EnvironmentVariables { get; } = [];
    public List<Type> EnumComponents { get; } = [];
    public API.Manila? ManilaAPI { get; private set; } = null;

    public ScriptContext(ILogger logger, IProfiler profiler, IExtensionManager extensionManager, string rootDir, string scriptPath) {
        _logger = logger;
        _profiler = profiler;
        _extensionManager = extensionManager;

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

        // Asynchronously initialize the script engine to avoid blocking
        _scriptEngine = Task.Run(() => CreateScriptEngine());
    }

    public string GetCompiledFilePath() {
        var fileName = $"{ScriptHash[0..16]}.bin";
        return Path.Join(_dataDir, "compiled", fileName);
    }

    public void Init(API.Manila manilaAPI, ScriptBridge bridge, Component component) {
        ManilaAPI = manilaAPI;
    }

    private V8ScriptEngine CreateScriptEngine() {
        using (new ProfileScope(_profiler, "Creating V8 Script Engine")) {
            var engine = new V8ScriptEngine(V8ScriptEngineFlags.EnableTaskPromiseConversion) {
                ExposeHostObjectStaticMembers = true
            };

            engine.AddHostObject("Manila", ManilaAPI);

            return engine;
        }
    }

    private async Task<string> CreateScriptCode() {
        _logger.Log(new ScriptUsingEntriesLogEntry(ScriptPath, ContextID));

        var code = @$"
            async function main() {{
                {await File.ReadAllTextAsync(ScriptPath)}
            }}

            main().then(() => {{
                __Manila_signalSuccess();
            }}).catch(err => {{
                __Manila_signalError(err.message);
            }});
        ";

        _logger.Log(new ScriptCodeCreatedLogEntry(ScriptPath, code, ContextID));

        return code;
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
        if (ManilaAPI == null) {
            throw new InternalLogicException("ManilaAPI is not initialized. Call Init() before executing the script.");
        }

        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            var startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _logger.Log(new ScriptExecutionStartedLogEntry(ScriptPath, ContextID));

            byte[]? cachedBytes = null;
            var codeTask = CreateScriptCode();
            var binPath = GetCompiledFilePath();

            try {
                await LoadEnvironmentVariablesAsync();
                var scriptChanged = CheckForScriptChanges(cache);

                if (!scriptChanged && File.Exists(binPath)) {
                    _logger.Debug($"Using cached compiled script from '{binPath}'.");
                    cachedBytes = await File.ReadAllBytesAsync(binPath);
                }
            } catch (ManilaException) {
                throw;
            } catch (Exception ex) {
                throw new InternalLogicException($"An unexpected error occurred during script execution of '{ScriptPath}'. See inner exception for details.", ex);
            }

            try {
                var engine = await _scriptEngine;
                var docInfo = new DocumentInfo(ScriptPath);
                var script = engine.Compile(docInfo, await codeTask, V8CacheKind.Code, cachedBytes, out var cacheAccepted);

                engine.AddHostObject("__Manila_signalSuccess", new Action(() => {
                    _logger.Debug($"Script '{ScriptPath}' signaled successful completion.");
                }));
                engine.AddHostObject("__Manila_signalError", new Action<object>((err) => {
                    _logger.Error($"Script '{ScriptPath}' signaled an error: {err}");
                    throw new ScriptExecutionException($"Script '{ScriptPath}' signaled an error: {err}", ScriptPath);
                }));

                if (!cacheAccepted) {
                    _logger.Warning($"Cached compiled script was rejected by V8 engine. Recompiling script '{ScriptPath}'.");

                    cache.AddOrUpdate(ScriptPath, ScriptHash);
                    script = engine.Compile(docInfo, await codeTask, V8CacheKind.Code, out var newBytes);
                    var directory = Path.GetDirectoryName(binPath)!;
                    if (!Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);
                    await File.WriteAllBytesAsync(binPath, newBytes);
                    _logger.Debug($"Wrote new compiled script to '{binPath}'.");
                }

                engine.Execute(script);
            } catch (ScriptEngineException ex) {
                throw new ScriptExecutionException($"Failed to compile or execute script: {ex.Message}", ScriptPath, ex);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                throw new EnvironmentException($"Failed to write script cache to '{binPath}'. Check directory permissions.", ex);
            }

            _logger.Log(new ScriptExecutedSuccessfullyLogEntry(ScriptPath, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, ContextID));
            component.Finalize(ManilaAPI);
        }
    }

    private bool CheckForScriptChanges(IFileHashCache cache) {
        return cache.HasChanged(ScriptPath, ScriptHash);
    }
}
