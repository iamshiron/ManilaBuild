using Shiron.Manila.API;
using Shiron.Manila.CPP.Components;
using Shiron.Manila.Utils;

namespace Shiron.Manila.CPP.Toolchain.Impl;

public class ToolchainClang : Toolchain {
    public class CompileCacheItem(string file, string objFile, long lastCompileTime, string lastCompileHash) {
        public string File { get; init; } = file;
        public string ObjFile { get; init; } = objFile;
        public long LastCompileTime { get; init; } = lastCompileTime;
        public string LastCompileHash { get; init; } = lastCompileHash;
        public List<string> Dependencies { get; init; } = [];
    }

    public class CompileStats() {
        public class CompileStatsItem(string file, string objFile, int compileDuration) {
            public string File { get; init; } = file;
            public string ObjFile { get; init; } = objFile;
            public int CompileDuration { get; init; } = compileDuration;

            public List<string> CompilerMessages { get; init; } = [];
            public List<string> CompilerWarnings { get; init; } = [];
            public List<string> CompilerErrors { get; init; } = [];
        }

        public int CacheHits { get; set; } = 0;
        public int CacheMisses { get; set; } = 0;
        public int CacheInvalidations { get; set; } = 0;

        public long BuildStartTime { get; set; } = 0;
        public long BuildEndTime { get; set; } = 0;
        public long BuildDuration { get; set; } = 0;

        public List<CompileStatsItem> Items { get; set; } = [];
    }

    private readonly List<string> _includeDirs = [];
    private readonly string _optimizationLevel = "";
    private readonly List<string> _links = [];
    private readonly List<string> _defines = [];
    private readonly List<string> _compileFlags = [];

    public readonly CompileStats Stats = new();
    public readonly List<CompileCacheItem> CompileCache = [];

    public ToolchainClang(Workspace workspace, Project project, BuildConfig config, bool invalidateCache = false) : base(workspace, project, config) {
        var comp = project.GetComponent<CppComponent>();
        _includeDirs.AddRange(comp.IncludeDirs);
        _links.AddRange(comp.Links);

        ManilaCPP.Instance.Debug("Include Dirs: " + string.Join(", ", _includeDirs));

        if (!invalidateCache) LoadCompileCache();
    }

    /// <summary>
    /// Invokes the compiler for C++ files.
    /// This method will create the output directory if it doesn't exist.
    /// </summary>
    /// <param name="a">Additional arguments</param>
    /// <returns>Exit code of the compiler</returns>
    public int InvokeCompiler(params string[] a) {
        List<string> args = [.. a];

        foreach (var i in _includeDirs) {
            args.Add("-I" + i);
        }
        foreach (var d in _defines) {
            args.Add("-D" + d);
        }

        return ShellUtils.Run("clang++", [.. args], Path.Join(workspace.Path),
            ManilaEngine.GetInstance().verboseLogger ? (s) => ApplicationLogger.WriteLine(s) : null, (s) => ApplicationLogger.ApplicationError(s)
        );
    }

    /// <summary>
    /// Invokes the linker for library components.
    /// This method will create the output directory if it doesn't exist.
    /// </summary>
    /// <param name="a">The arguments</param>
    /// <returns>Exit code of the linker</returns>
    public int InvokeLibLinker(params string[] a) {
        List<string> args = [.. a];
        return ShellUtils.Run("llvm-ar", [.. args], Path.Join(workspace.Path),
            ManilaEngine.GetInstance().verboseLogger ? (s) => ApplicationLogger.WriteLine(s) : null, (s) => ApplicationLogger.ApplicationError(s)
        );
    }

    /// <summary>
    /// Invokes the linker for the application.
    /// This method will create the output directory if it doesn't exist.
    /// </summary>
    /// <param name="a">The arguments</param>
    /// <returns>Exit code of the linker</returns>
    public int InvokeAppLinker(params string[] a) {
        List<string> args = [.. a];

        foreach (var l in _links) {
            args.Add("-l" + l);
        }

        return ShellUtils.Run("clang++", [.. args], Path.Join(workspace.Path),
            ManilaEngine.GetInstance().verboseLogger ? (s) => ApplicationLogger.WriteLine(s) : null, (s) => ApplicationLogger.ApplicationError(s)
        );
    }

    /// <summary>
    /// Compiles the given file into an object file.
    /// This method will create the output directory if it doesn't exist.
    /// </summary>
    /// <param name="setRoot">The root of the source set</param>
    /// <param name="file">The file inside the source set</param>
    /// <param name="objDir">The intermediate directory</param>
    /// <returns>Filepath to the compiled intermediate file</returns>
    public string CompileFile(string setRoot, string file, string objDir) {
        var objFile = Path.Combine(objDir, Path.GetFileNameWithoutExtension(file) + ".o");
        if (!Directory.Exists(Path.GetDirectoryName(objFile))) {
            Directory.CreateDirectory(Path.GetDirectoryName(objFile)!);
        }

        // Check if we can use cached version
        if (NeedsRecompilation(file, objFile, out var cacheItem)) {
            // Start timing the compilation
            var startTime = System.Diagnostics.Stopwatch.StartNew();

            // Run the compiler
            ApplicationLogger.ApplicationLog(Path.GetRelativePath(setRoot, file));
            if (InvokeCompiler("-c", file, "-o", objFile) != 0) {
                throw new Exception("Failed to compile file: " + file);
            }

            // Stop timing
            startTime.Stop();
            int compileDuration = (int) startTime.ElapsedMilliseconds;

            // Create a new cache entry
            var newCacheItem = new CompileCacheItem(
                file,
                objFile,
                DateTime.Now.Ticks,
                GetFileHash(file)
            );

            // Add dependencies to the cache entry
            var dependencies = GetHeaderDependencies(file);
            foreach (var dep in dependencies) {
                newCacheItem.Dependencies.Add(dep);
            }

            // Remove any existing cache entry for this file
            CompileCache.RemoveAll(c => c.File == file);

            // Add the new cache entry
            CompileCache.Add(newCacheItem);

            // Update stats
            Stats.CacheMisses++;
            Stats.Items.Add(new CompileStats.CompileStatsItem(file, objFile, compileDuration));

            ManilaCPP.Instance.Debug($"Compiled {file} in {compileDuration}ms");
        } else if (cacheItem != null) {
            // Using cached object file
            // We only need to ensure the object file exists (it might have been deleted)
            if (!System.IO.File.Exists(cacheItem.ObjFile)) {
                ManilaCPP.Instance.Debug($"Cache entry exists but object file missing. Recompiling {file}.");
                // If the object file is missing, we need to recompile
                return CompileFile(setRoot, file, objDir);
            }

            // Update stats for cached compilation
            Stats.Items.Add(new CompileStats.CompileStatsItem(file, objFile, 0) {
                CompilerMessages = ["Using cached version"]
            });
        }

        return objFile;
    }

    /// <summary>
    /// Links the given files into a single output file.
    /// This method will create the output directory if it doesn't exist.
    /// </summary>
    /// <param name="files">The intermediate files</param>
    /// <param name="binDir">The output directory</param>
    /// <param name="comp">The cpp component</param>
    /// <param name="a">Additional arguments</param>
    /// <returns>The path to the compiled file</returns>
    public string LinkFiles(List<string> files, string binDir, CppComponent comp, params string[] a) {
        var outFile = Utils.GetBinFile(project, project.GetComponent<CppComponent>());
        List<string> args = [.. a, .. files];

        if (!Directory.Exists(Path.GetDirectoryName(outFile))) {
            Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        }

        if (comp is StaticLibComponent) {
            InvokeLibLinker("rc", outFile, string.Join(" ", files));
            return outFile;
        } else if (comp is ConsoleComponent) {
            InvokeAppLinker(string.Join(" ", files), "-o", outFile);
        }

        return outFile;
    }

    /// <summary>
    /// Builds the project using the Clang toolchain.
    /// </summary>
    /// <param name="workspace">The workspace</param>
    /// <param name="project">The project</param>
    /// <param name="config">The build config</param>
    /// <exception cref="Exception">If anything goes kaoboom</exception>
    public override void Build(Workspace workspace, Project project, BuildConfig config) {
        var instance = ManilaCPP.Instance;
        instance.Info($"Building {project.Name} with Clang toolchain.");

        List<string> objectFiles = [];
        var cppComponent = (CppComponent?) (project.HasComponent<ConsoleComponent>() ? project.GetComponent<ConsoleComponent>() : project.HasComponent<StaticLibComponent>() ? project.GetComponent<StaticLibComponent>() : null);
        if (cppComponent == null) throw new Exception("No C++ component found in project.");
        var objDir = cppComponent.ObjDir!;
        var binDir = cppComponent.BinDir!;

        var sourceSet = project._sourceSets["main"];
        foreach (var file in sourceSet.files()) {
            var f = Path.Join(sourceSet.Root, file.path);
            objectFiles.Add(CompileFile(sourceSet.Root, f, Path.Join(objDir, sourceSet.Root)));
        }

        LinkFiles(objectFiles, binDir, cppComponent);
        WriteCompileCache();
    }

    /// <summary>
    /// Returns the path to the compile cache file for this project.
    /// </summary>
    /// <returns>The compile cache file</returns>
    public string GetCompileCacheFile() {
        return Path.Join(ManilaCPP.Instance.GetDataDir(), "clang_cache", $"{project.Name}.bin");
    }

    /// <summary>
    /// Generates a SHA256 hash for the given file path.
    /// </summary>
    /// <param name="file">The file</param>
    /// <returns>The SHA256 hash</returns>
    public static string GetFileHash(string file) {
        return HashUtils.SHA256(file);
    }

    /// <summary>
    /// Writes the compile cache to a file.
    /// </summary>
    public void WriteCompileCache() {
        try {
            string cacheFile = GetCompileCacheFile();
            string cacheDir = Path.GetDirectoryName(cacheFile)!;

            if (!Directory.Exists(cacheDir)) {
                Directory.CreateDirectory(cacheDir);
            }

            using var fs = new FileStream(cacheFile, FileMode.Create);
            using var writer = new BinaryWriter(fs);

            // Write a simple header with version info
            writer.Write("CLANGCACHE"); // 10 bytes signature
            writer.Write((int) 1); // Version number

            // Write number of cache items
            writer.Write(CompileCache.Count);

            // Write each cache item
            foreach (var item in CompileCache) {
                writer.Write(item.File);
                writer.Write(item.ObjFile);
                writer.Write(item.LastCompileTime);
                writer.Write(item.LastCompileHash);

                // Write dependencies
                writer.Write(item.Dependencies.Count);
                foreach (var dep in item.Dependencies) {
                    writer.Write(dep);
                }
            }

            ManilaCPP.Instance.Debug($"Wrote {CompileCache.Count} items to compile cache: {cacheFile}");
        } catch (Exception ex) {
            ManilaCPP.Instance.Debug($"Error writing compile cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the compile cache from the file.
    /// </summary>
    public void LoadCompileCache() {
        string cacheFile = GetCompileCacheFile();

        if (!System.IO.File.Exists(cacheFile)) {
            ManilaCPP.Instance.Debug($"No compile cache file found at {cacheFile}");
            return;
        }

        try {
            using var fs = new FileStream(cacheFile, FileMode.Open);
            using var reader = new BinaryReader(fs);            // Read and verify header
            string signature = reader.ReadString() ?? "";
            if (signature != "CLANGCACHE") {
                ManilaCPP.Instance.Debug("Invalid cache file format");
                return;
            }

            int version = reader.ReadInt32();
            if (version != 1) {
                ManilaCPP.Instance.Debug($"Unsupported cache version: {version}");
                return;
            }

            // Read number of items
            int itemCount = reader.ReadInt32();
            CompileCache.Clear();

            // Read each cache item
            for (int i = 0; i < itemCount; i++) {
                string file = reader.ReadString();
                string objFile = reader.ReadString();
                long lastCompileTime = reader.ReadInt64();
                string lastCompileHash = reader.ReadString();

                var item = new CompileCacheItem(file, objFile, lastCompileTime, lastCompileHash);

                // Read dependencies
                int depCount = reader.ReadInt32();
                for (int j = 0; j < depCount; j++) {
                    string dep = reader.ReadString();
                    item.Dependencies.Add(dep);
                }

                CompileCache.Add(item);
            }

            ManilaCPP.Instance.Debug($"Loaded {CompileCache.Count} items from compile cache");
        } catch (Exception ex) {
            ManilaCPP.Instance.Debug($"Error loading compile cache: {ex.Message}");
            // Start with an empty cache on error
            CompileCache.Clear();
        }
    }

    /// <summary>
    /// Recursively extracts header dependencies from a source file.
    /// This method will traverse the include tree and collect all headers that are included by the given file.
    /// </summary>
    /// <param name="filePath">The file</param>
    /// <returns>List of all included files</returns>
    private List<string> GetHeaderDependencies(string filePath) {
        List<string> dependencies = [];
        HashSet<string> processedFiles = new(); // To prevent circular includes

        GetHeaderDependenciesRecursive(filePath, dependencies, processedFiles);

        // Remove duplicates and return
        return dependencies.Distinct().ToList();
    }

    /// <summary>
    /// Recursively extracts header dependencies from a source file.
    /// This method will traverse the include tree and collect all headers that are included by the given file.
    /// </summary>
    /// <param name="filePath">The file</param>
    /// <param name="dependencies">The found dependencies</param>
    /// <param name="processedFiles">The processed files</param>
    private void GetHeaderDependenciesRecursive(string filePath, List<string> dependencies, HashSet<string> processedFiles) {
        if (!System.IO.File.Exists(filePath) || processedFiles.Contains(filePath)) {
            return;
        }

        // Mark this file as processed to prevent circular includes
        processedFiles.Add(filePath);

        try {
            string[] lines = System.IO.File.ReadAllLines(filePath);
            foreach (string line in lines) {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("#include")) {
                    // Extract the header path from the include directive
                    int startIdx = trimmedLine.IndexOf('"');
                    int endIdx = -1;
                    string headerPath = string.Empty;

                    if (startIdx != -1) {
                        // Local include with quotes: #include "header.hpp"
                        endIdx = trimmedLine.IndexOf('"', startIdx + 1);
                        if (endIdx != -1) {
                            headerPath = trimmedLine.Substring(startIdx + 1, endIdx - startIdx - 1);
                            string resolvedPath = ResolveHeaderPath(filePath, headerPath);
                            if (System.IO.File.Exists(resolvedPath) && !processedFiles.Contains(resolvedPath)) {
                                dependencies.Add(resolvedPath);
                                // Recursively get dependencies of this header
                                GetHeaderDependenciesRecursive(resolvedPath, dependencies, processedFiles);
                            }
                        }
                    } else {
                        // System include with angle brackets: #include <header>
                        startIdx = trimmedLine.IndexOf('<');
                        if (startIdx != -1) {
                            endIdx = trimmedLine.IndexOf('>', startIdx + 1);
                            if (endIdx != -1) {
                                headerPath = trimmedLine.Substring(startIdx + 1, endIdx - startIdx - 1);
                                // Try to find system headers in include directories
                                foreach (var includeDir in _includeDirs) {
                                    string potentialPath = Path.Combine(includeDir, headerPath);
                                    if (System.IO.File.Exists(potentialPath) && !processedFiles.Contains(potentialPath)) {
                                        dependencies.Add(potentialPath);
                                        // Also get dependencies of this system header
                                        GetHeaderDependenciesRecursive(potentialPath, dependencies, processedFiles);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        } catch (Exception ex) {
            ManilaCPP.Instance.Debug($"Error extracting dependencies from {filePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves header paths relative to the source file's directory.
    /// If the header is not found in the same directory, it will check the include directories.
    /// </summary>
    /// <param name="sourceFilePath">The source path</param>
    /// <param name="headerPath">The header path</param>
    /// <returns>The absolute header path</returns>
    private string ResolveHeaderPath(string sourceFilePath, string headerPath) {
        string sourceDir = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;
        string absolutePath = Path.GetFullPath(Path.Combine(sourceDir, headerPath));

        // If direct resolution doesn't work, try include directories
        if (!System.IO.File.Exists(absolutePath)) {
            foreach (var includeDir in _includeDirs) {
                string potentialPath = Path.Combine(includeDir, headerPath);
                if (System.IO.File.Exists(potentialPath)) {
                    return potentialPath;
                }
            }
        }

        return absolutePath;
    }

    /// <summary>
    /// Checks if the file needs recompilation based on the cache and file modification times.
    /// If the object file doesn't exist or the source file or any of its dependencies have changed, it returns true.
    /// </summary>
    /// <param name="filePath">The source file</param>
    /// <param name="objFile">The intermediate file</param>
    /// <param name="cacheItem">The cache object for this file</param>
    /// <returns>True: file needs to be recompiled</returns>
    private bool NeedsRecompilation(string filePath, string objFile, out CompileCacheItem? cacheItem) {
        cacheItem = null;

        // If object file doesn't exist, definitely need to compile
        if (!System.IO.File.Exists(objFile)) {
            ManilaCPP.Instance.Debug($"Object file {objFile} does not exist. Compiling {filePath}.");
            return true;
        }

        // Check if we have a cache entry
        var existingCache = CompileCache.FirstOrDefault(c => c.File == filePath);
        if (existingCache == null) {
            ManilaCPP.Instance.Debug($"No cache entry found for {filePath}. Compiling.");
            return true;
        }

        // Check if the source file itself has changed
        string currentHash = GetFileHash(filePath);
        if (currentHash != existingCache.LastCompileHash) {
            ManilaCPP.Instance.Debug($"Source file {filePath} has changed (hash mismatch). Recompiling.");
            Stats.CacheInvalidations++;
            return true;
        }

        // Check if any dependency has changed since last compile
        foreach (string dependency in existingCache.Dependencies) {
            if (!System.IO.File.Exists(dependency)) {
                // Dependency no longer exists
                ManilaCPP.Instance.Debug($"Dependency {dependency} no longer exists. Recompiling {filePath}.");
                Stats.CacheInvalidations++;
                return true;
            }

            long lastModified = new System.IO.FileInfo(dependency).LastWriteTime.Ticks;
            if (lastModified > existingCache.LastCompileTime) {
                // A dependency was modified after the last compilation
                ManilaCPP.Instance.Debug($"Dependency {dependency} changed. Recompiling {filePath}.");
                Stats.CacheInvalidations++;
                return true;
            }
        }

        // File and all dependencies are up to date
        cacheItem = existingCache;
        Stats.CacheHits++;
        ManilaCPP.Instance.Debug($"Using cached object file for {filePath}.");
        return false;
    }
}
