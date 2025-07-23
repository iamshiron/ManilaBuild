
public static class CommandUtils {
    public static string[] GetCommandNames(string[] args) {
        return [.. args
            .Where(arg => !arg.StartsWith('-') && !arg.StartsWith('/'))
            .Select(arg => arg.Trim().ToLower())];
    }
}
