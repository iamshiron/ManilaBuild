namespace Shiron.Manila.API;

// As class is exposed to the scripting environment, use JavaScript naming conventions
#pragma warning disable IDE1006

/// <summary>
/// Represents a directory in the scripting context. Mostly used for syntax sugar.
/// </summary>
public class DirHandle {
    public string path { get; private set; }

    public DirHandle(string path) {
        this.path = path;
    }

    public FileHandle[] files() {
        string[] files = Directory.GetFiles(this.path);
        FileHandle[] result = new FileHandle[files.Length];
        for (int i = 0; i < files.Length; i++) {
            result[i] = new FileHandle(files[i]);
        }
        return result;
    }

    public FileHandle file(string name) {
        return new FileHandle(Path.Combine(this.path, name));
    }

    public DirHandle join(params object[] path) {
        var newPath = this.path;
        foreach (var p in path) {
            newPath = Path.Combine(newPath, p.ToString());
        }
        return new DirHandle(newPath);
    }
    public DirHandle join(DirHandle dir) {
        return new DirHandle(Path.Combine(this.path, dir.path));
    }

    public bool isAbsolute() {
        return Path.IsPathRooted(this.path);
    }
    public bool exists() {
        return Directory.Exists(this.path);
    }
    public void create() {
        Directory.CreateDirectory(this.path);
    }

    public string get() {
        return this.path;
    }

    public static implicit operator string(DirHandle d) {
        return d.path;
    }
}
