using Spectre.Console;
using System;

namespace Shiron.Manila.CLI;

/// <summary>
/// A static utility class containing helper methods.
/// </summary>
public static class Utils {
    /// <summary>
    /// Attempts to write a formatted exception to the console using Spectre.Console.
    /// </summary>
    /// <remarks>
    /// If <see cref="AnsiConsole.WriteException(Exception)"/> fails for any reason,
    /// it will fall back to the standard <see cref="Console.WriteLine(object)"/> to ensure the exception is still logged.
    /// </remarks>
    /// <param name="e">The exception to be written to the console.</param>
    public static void TryWriteException(Exception e) {
        try {
            AnsiConsole.WriteException(e);
        } catch {
            Console.WriteLine(e);
        }
    }
}
