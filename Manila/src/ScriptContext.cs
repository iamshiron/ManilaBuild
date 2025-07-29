using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Shiron.Manila.API;
using Shiron.Manila.API.Bridges;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Logging;
using Shiron.Manila.Caching;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;
using Shiron.Manila.Profiling;
using Shiron.Manila.Services;
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
    }

    public string GetCompiledFilePath() {
        var fileName = $"{ScriptHash[0..16]}.dll";
        return Path.Join(_dataDir, "compiled", fileName);
    }

    public void Init(API.Manila manilaAPI, ScriptBridge bridge, Component component) {
        ManilaAPI = manilaAPI;
    }

    private async Task<string> CreateScriptCode(List<string> usings) {
        _logger.Debug($"Appending {usings.Count} usings to script code.");
        _logger.Debug($"Usings: {string.Join(", ", usings)}");
        _logger.Log(new ScriptUsingEntriesLogEntry(ScriptPath, [.. usings], ContextID));

        var code = @$"
            {string.Join("\n", usings.Select(u => $"using {u};"))}

            public class Script : IScriptEntry {{
                public async Task ExecuteAsync(Shiron.Manila.API.Manila Manila) {{
                    {await File.ReadAllTextAsync(ScriptPath)}
                }}
            }}
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

            try {
                await LoadEnvironmentVariablesAsync();

                var assemblyPath = GetCompiledFilePath();
                bool scriptChanged = CheckForScriptChanges(cache);

                Assembly? assembly = null;
                if (!scriptChanged && File.Exists(assemblyPath)) {
                    _logger.Log(new ScriptAssemblyCacheHitLogEntry(ScriptPath, assemblyPath, ContextID));

                    var context = new PluginLoadContext(_profiler, assemblyPath);
                    assembly = Assembly.LoadFrom(assemblyPath);
                } else {
                    _logger.Log(new ScriptAssemblyCacheMissEntry(ScriptPath, assemblyPath, ContextID));
                    assembly = await CompileAndCacheScriptAsync(cache, assemblyPath);
                }

                if (assembly == null) {
                    throw new ScriptExecutionException($"Failed to load or compile script assembly for '{ScriptPath}'.");
                }

                await InvokeScriptEntryPoint(assembly, ManilaAPI, component);

                component.Finalize(ManilaAPI);
            } catch (ManilaException) {
                throw;
            } catch (Exception ex) {
                throw new InternalLogicException($"An unexpected error occurred during script execution of '{ScriptPath}'. See inner exception for details.", ex);
            }

            _logger.Log(new ScriptExecutedSuccessfullyLogEntry(ScriptPath, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime, ContextID));
        }
    }

    private async Task<Assembly> CompileAndCacheScriptAsync(IFileHashCache cache, string assemblyPath) {
        using (new ProfileScope(_profiler, "Compiling Script")) {
            List<string> namespaces = [
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "System.Threading.Tasks",
                "Shiron.Manila",
                "Shiron.Manila.API",
                "Shiron.Manila.API.Interfaces",
                "Shiron.Manila.API.Bridges"
            ];

            namespaces.AddRange(_extensionManager.ExposedTypes
                .Where(t => t.Namespace != null)
                .Select(t => t.Namespace!)
                .Distinct()
            );

            var code = await CreateScriptCode(namespaces);
            var references = new List<MetadataReference> {
                // CS0656 - Missing compiler required member 'Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo.Create'
                MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly.Location),
            };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                try {
                    if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location)) {
                        _logger.Debug($"Adding reference to assembly: {assembly.FullName} from {assembly.Location}");
                        references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    }
                } catch (NotSupportedException) {
                    // Ignore dynamic assemblies or assemblies loaded from byte arrays
                }
            }

            var csharpAssembly = typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly;
            if (!references.Any(r => r.Display != null && r.Display.Contains("Microsoft.CSharp.dll"))) {
                _logger.Debug($"Adding reference to Microsoft.CSharp.dll from {csharpAssembly.Location}");
                references.Add(MetadataReference.CreateFromFile(csharpAssembly.Location));
            }

            var compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(assemblyPath),
                syntaxTrees: [CSharpSyntaxTree.ParseText(code)],
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
                    {
                        {"CS1701", ReportDiagnostic.Suppress},
                        {"CS1702", ReportDiagnostic.Suppress},
                        {"CS0246", ReportDiagnostic.Suppress}
                    })
            );

            using var ms = new MemoryStream();
            var res = compilation.Emit(ms);

            if (!res.Success) {
                var errors = res.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
                var e = new ScriptCompilationException("Script compilation failed.", errors);
                _logger.Log(new ScriptCompilationFailedLogEntry(ScriptPath, e, ContextID));
                throw e;
            }

            _ = ms.Seek(0, SeekOrigin.Begin);
            await File.WriteAllBytesAsync(assemblyPath, ms.ToArray());
            cache.AddOrUpdate(ScriptPath, ScriptHash);

            _logger.Log(new ScriptCompiledLogEntry(ScriptPath, assemblyPath, ContextID));

            _ = ms.Seek(0, SeekOrigin.Begin);
            return Assembly.Load(ms.ToArray());
        }
    }

    private async Task InvokeScriptEntryPoint(Assembly assembly, API.Manila manilaAPI, Component component) {
        using (new ProfileScope(_profiler, "Invoking Script Entry Point")) {
            IScriptEntry? script = null;
            foreach (var t in assembly.GetTypes()) {
                if (typeof(IScriptEntry).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract) {
                    try {
                        script = (IScriptEntry) Activator.CreateInstance(t)!;
                        break;
                    } catch (Exception ex) {
                        _logger.Error($"Failed to instantiate script entry point '{t.FullName}': {ex.Message}");
                    }
                }
            }

            if (script == null) {
                throw new ScriptExecutionException($"No valid script entry point found in assembly '{assembly.FullName}' for script '{ScriptPath}'.");
            }

            await script.ExecuteAsync(manilaAPI);
        }
    }

    private bool CheckForScriptChanges(IFileHashCache cache) {
        return cache.HasChanged(ScriptPath, ScriptHash);
    }
}
