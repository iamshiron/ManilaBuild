using Shiron.Manila.API;
using Shiron.Manila.CPP.Components;
using Shiron.Manila.Utils;

namespace Shiron.Manila.CPP.Toolchain.Impl;

public class ToolchainClang : Toolchain {
    private readonly List<string> _includeDirs = [];
    private readonly string _optimizationLevel = "";
    private readonly List<string> _links = [];
    private readonly List<string> _defines = [];

    public ToolchainClang(Workspace workspace, Project project, BuildConfig config) : base(workspace, project, config) {
        var comp = project.GetComponent<CppComponent>();
        _includeDirs.AddRange(comp.IncludeDirs);
        _links.AddRange(comp.Links);

        ManilaCPP.Instance.Debug("Include Dirs: " + string.Join(", ", _includeDirs));
    }

    public int RunCompiler(params string[] a) {
        List<string> args = [.. a];

        foreach (var i in _includeDirs) {
            args.Add("-I" + i);
        }
        foreach (var d in _defines) {
            args.Add("-D" + d);
        }

        return ShellUtils.Run("clang++", [.. args], Path.Join(workspace.Path));
    }
    public int RunStaticLibLinker(params string[] a) {
        List<string> args = [.. a];
        return ShellUtils.Run("llvm-ar", [.. args], Path.Join(workspace.Path));
    }
    public int RunApplicationLiunker(params string[] a) {
        List<string> args = [.. a];

        foreach (var l in _links) {
            args.Add("-l" + l);
        }

        return ShellUtils.Run("clang++", [.. args], Path.Join(workspace.Path));
    }

    public string CompileFile(string file, string objDir) {
        var objFile = Path.Combine(objDir, Path.GetFileNameWithoutExtension(file) + ".o");
        if (!Directory.Exists(Path.GetDirectoryName(objFile))) {
            Directory.CreateDirectory(Path.GetDirectoryName(objFile)!);
        }

        RunCompiler("-c", file, "-o", objFile);
        return objFile;
    }
    public string LinkFiles(List<string> files, string binDir, CppComponent comp, params string[] a) {
        var outFile = Utils.GetBinFile(project, project.GetComponent<CppComponent>());
        List<string> args = [.. a, .. files];

        if (!Directory.Exists(Path.GetDirectoryName(outFile))) {
            Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        }

        if (comp is StaticLibComponent) {
            RunStaticLibLinker("rc", outFile, string.Join(" ", files));
            return outFile;
        } else if (comp is ConsoleComponent) {
            RunApplicationLiunker(string.Join(" ", files), "-o", outFile);
        }

        return outFile;
    }

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
            objectFiles.Add(CompileFile(f, Path.Join(objDir, sourceSet.Root)));
        }

        LinkFiles(objectFiles, binDir, cppComponent);
    }
}
