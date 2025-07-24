using System.Diagnostics.CodeAnalysis;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a file handle for scripting operations.
/// </summary>
public class FileHandle {
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string Handle { get; private set; }

    /// <summary>
    /// Initializes a new file handle from directory and name.
    /// </summary>
    /// <param name="dir">The directory path.</param>
    /// <param name="name">The file name.</param>
    public FileHandle(string dir, string name) {
        this.Handle = Path.Combine(dir, name);
    }

    /// <summary>
    /// Initializes a new file handle from a relative path.
    /// </summary>
    /// <param name="path">The file path relative to current directory.</param>
    public FileHandle(string path) {
        this.Handle = Path.Combine(Directory.GetCurrentDirectory(), path);
    }

    /// <summary>
    /// Gets the directory containing this file.
    /// </summary>
    /// <returns>A directory handle for the parent directory.</returns>
    public DirHandle GetDir() {
        var dirName = Path.GetDirectoryName(this.Handle) ?? string.Empty;
        return new DirHandle(dirName);
    }

    /// <summary>
    /// Checks if the file path is absolute.
    /// </summary>
    /// <returns>True if the path is absolute, false otherwise.</returns>
    public bool IsAbsolute() {
        return Path.IsPathRooted(this.Handle);
    }

    /// <summary>
    /// Checks if the file exists on disk.
    /// </summary>
    /// <returns>True if the file exists, false otherwise.</returns>
    public bool Exists() {
        return File.Exists(this.Handle);
    }

    /// <summary>
    /// Creates the file on disk.
    /// </summary>
    public void Create() {
        File.Create(this.Handle);
    }

    /// <summary>
    /// Gets the file path as a string.
    /// </summary>
    /// <returns>The file path.</returns>
    public string Get() {
        return this.Handle;
    }

    /// <summary>
    /// Implicitly converts the file handle to a string.
    /// </summary>
    /// <param name="f">The file handle to convert.</param>
    /// <returns>The file path as a string.</returns>
    public static implicit operator string(FileHandle f) {
        return f.Handle;
    }

    /// <summary>
    /// Returns the file path as a string.
    /// </summary>
    /// <returns>The file path.</returns>
    public override string ToString() {
        return Handle;
    }
}
