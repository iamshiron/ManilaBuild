
using Spectre.Console;

public static class ExceptionUtils {
    /// <summary>
    /// Attempts to write a formatted exception to the console using Spectre.Console.
    /// </summary>
    /// <remarks>
    /// If <see cref="AnsiConsole.ManilaException(Exception)"/> fails for any reason,
    /// it will fall back to the standard <see cref="Console.WriteLine(object)"/> to ensure the exception is still logged.
    /// </remarks>
    /// <param name="e">The exception to be written to the console.</param>
    public static void ManilaException(Exception e) {
        try {
            AnsiConsole.ManilaException(e);
        } catch {
            Console.WriteLine(e);
        }
    }
}
