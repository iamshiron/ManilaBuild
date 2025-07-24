using System.Diagnostics.CodeAnalysis;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a directory in the scripting context for syntax sugar and convenience operations.
/// </summary>
public class DirHandle {
    /// <summary>
    /// The absolute path to the directory.
    /// </summary>
    public string Handle { get; private set; }

    /// <summary>
    /// Initializes a new directory handle with the specified path.
    /// </summary>
    /// <param name="path">The directory path (relative or absolute).</param>
    public DirHandle(string path) {
        if (System.IO.Path.IsPathFullyQualified(path)) Handle = path;
        else Handle = Path.Join(Directory.GetCurrentDirectory(), path);
    }

    /// <summary>
    /// Gets all files in this directory as FileHandle objects.
    /// </summary>
    /// <returns>Array of file handles for all files in the directory.</returns>
    public FileHandle[] Files() {
        string[] files = Directory.GetFiles(this.Handle);
        FileHandle[] result = new FileHandle[files.Length];
        for (int i = 0; i < files.Length; i++) {
            result[i] = new FileHandle(files[i]);
        }
        return result;
    }

    /// <summary>
    /// Creates a file handle for a file within this directory.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <returns>A file handle for the specified file.</returns>
    public FileHandle File(string name) {
        return new FileHandle(System.IO.Path.Combine(this.Handle, name));
    }

    /// <summary>
    /// Combines this directory path with additional path segments.
    /// </summary>
    /// <param name="path">Path segments to join.</param>
    /// <returns>A new directory handle with the combined path.</returns>
    public DirHandle Join(params object[] path) {
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
    /// <summary>
    /// Combines this directory path with another directory handle.
    /// </summary>
    /// <param name="dir">The directory handle to join with.</param>
    /// <returns>A new directory handle with the combined path.</returns>
    public DirHandle Join(DirHandle dir) {
        return new DirHandle(System.IO.Path.Combine(this.Handle, dir.Handle));
    }

    /// <summary>
    /// Checks if this directory path is absolute.
    /// </summary>
    /// <returns>True if the path is absolute, false otherwise.</returns>
    public bool IsAbsolute() {
        return System.IO.Path.IsPathRooted(this.Handle);
    }
    /// <summary>
    /// Checks if this directory exists on the file system.
    /// </summary>
    /// <returns>True if the directory exists, false otherwise.</returns>
    public bool Exists() {
        return Directory.Exists(this.Handle);
    }
    /// <summary>
    /// Creates this directory on the file system.
    /// </summary>
    public void Create() {
        _ = Directory.CreateDirectory(this.Handle);
    }

    /// <summary>
    /// Gets the directory path as a string.
    /// </summary>
    /// <returns>The directory path.</returns>
    public string Get() {
        return this.Handle;
    }

    /// <summary>
    /// Implicitly converts a DirHandle to its string path representation.
    /// </summary>
    /// <param name="d">The directory handle to convert.</param>
    public static implicit operator string(DirHandle d) {
        return d.Handle;
    }
    /// <summary>
    /// Returns the string representation of this directory handle.
    /// </summary>
    /// <returns>The directory path.</returns>
    public override string ToString() {
        return Handle;
    }
}
