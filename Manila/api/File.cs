namespace Shiron.Manila.API;

// As class is exposed to the scripting environment, use JavaScript naming conventions
#pragma warning disable IDE1006

/// <summary>
/// Represents a file in the scripting context. Mostly used for syntax sugar.
/// </summary>
public class FileHandle {
    public string path { get; private set; }

    public FileHandle(string dir, string name) {
        this.path = Path.Combine(dir, name);
    }
    public FileHandle(string path) {
        this.path = path;
    }

    public DirHandle getDir() {
        return new DirHandle(Path.GetDirectoryName(this.path));
    }

    public bool isAbsolute() {
        return Path.IsPathRooted(this.path);
    }
    public bool exists() {
        return System.IO.File.Exists(this.path);
    }
    public void create() {
        System.IO.File.Create(this.path);
    }

    public string get() {
        return this.path;
    }

    public static implicit operator string(FileHandle f) {
        return f.path;
    }
}
