using Shiron.Manila.API;
using Shiron.Manila.CPP.Components;
using Shiron.Manila.Ext;

namespace Shiron.Manila.CPP;

public static class Utils {
    public static string GetBinFile(Project project, CppComponent c) {
        string extension = c is StaticLibComponent ? ".lib" : project.HasComponent<ConsoleComponent>() ? ".exe" : ".o";
        string binFile = project.Name + extension;

        return Path.Join(c.BinDir!, binFile);
    }
}
