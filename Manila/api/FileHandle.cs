namespace Shiron.Manila.API;

// As class is exposed to the scripting environment, use JavaScript naming conventions
#pragma warning disable IDE1006

/// <summary>
/// Represents a file in the scripting context. Mostly used for syntax sugar.
/// </summary>
public class FileHandle {
    public string Handle { get; private set; }

    public FileHandle(string dir, string name) {
        this.Handle = Path.Combine(dir, name);
    }
    public FileHandle(string path) {
        this.Handle = Path.Combine(Directory.GetCurrentDirectory(), path);
    }

    public DirHandle getDir() {
        var dirName = Path.GetDirectoryName(this.Handle) ?? string.Empty;
        return new DirHandle(dirName);
    }

    public bool isAbsolute() {
        return Path.IsPathRooted(this.Handle);
    }
    public bool exists() {
        return System.IO.File.Exists(this.Handle);
    }
    public void create() {
        System.IO.File.Create(this.Handle);
    }

    public string get() {
        return this.Handle;
    }

    public static implicit operator string(FileHandle f) {
        return f.Handle;
    }
    public override string ToString() {
        return Handle;
    }
}
