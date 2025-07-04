namespace Shiron.Manila.API;

// As class is exposed to the scripting environment, use JavaScript naming conventions
#pragma warning disable IDE1006

/// <summary>
/// Represents a directory in the scripting context. Mostly used for syntax sugar.
/// </summary>
public class DirHandle {
    public string Handle { get; private set; }

    public DirHandle(string path) {
        if (System.IO.Path.IsPathFullyQualified(path)) Handle = path;
        else Handle = Path.Join(Directory.GetCurrentDirectory(), path);
    }

    public FileHandle[] files() {
        string[] files = Directory.GetFiles(this.Handle);
        FileHandle[] result = new FileHandle[files.Length];
        for (int i = 0; i < files.Length; i++) {
            result[i] = new FileHandle(files[i]);
        }
        return result;
    }

    public FileHandle file(string name) {
        return new FileHandle(System.IO.Path.Combine(this.Handle, name));
    }

    public DirHandle join(params object[] path) {
        var newPath = this.Handle;
        foreach (var p in path) {
            if (p != null) {
                var pStr = p.ToString();
                if (!string.IsNullOrEmpty(pStr))
                    newPath = System.IO.Path.Combine(newPath, pStr);
            }
        }
        return new DirHandle(newPath);
    }
    public DirHandle join(DirHandle dir) {
        return new DirHandle(System.IO.Path.Combine(this.Handle, dir.Handle));
    }

    public bool isAbsolute() {
        return System.IO.Path.IsPathRooted(this.Handle);
    }
    public bool exists() {
        return Directory.Exists(this.Handle);
    }
    public void create() {
        Directory.CreateDirectory(this.Handle);
    }

    public string get() {
        return this.Handle;
    }

    public static implicit operator string(DirHandle d) {
        return d.Handle;
    }
    public override string ToString() {
        return Handle;
    }
}
