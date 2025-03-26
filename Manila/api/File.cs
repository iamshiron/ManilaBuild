namespace Shiron.Manila.API;

// As class is exposed to the scripting environment, use JavaScript naming conventions
#pragma warning disable IDE1006

/// <summary>
/// Represents a file in the scripting context. Mostly used for syntax sugar.
/// </summary>
public class File {
    public string path { get; private set; }

    public File(string dir, string name) {
        this.path = Path.Combine(dir, name);
    }
    public File(string path) {
        this.path = path;
    }

    public Dir getDir() {
        return new Dir(Path.GetDirectoryName(this.path));
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

    public static implicit operator string(File f) {
        return f.path;
    }
}
