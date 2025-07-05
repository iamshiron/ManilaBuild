namespace Shiron.Manila.Exceptions;

/// <summary>
/// Represents errors related to user configuration, such as invalid settings
/// in a Manila.js file, incorrect project structure, or missing plugins.
/// </summary>
public class ConfigurationException : ManilaException {
    public ConfigurationException(string message) : base(message) { }

    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Thrown when a requested plugin cannot be found by the ExtensionManager.
/// </summary>
public class PluginNotFoundException : ConfigurationException {
    public string PluginKey { get; }

    public PluginNotFoundException(string pluginKey)
        : base($"Plugin with key '{pluginKey}' was not found.") {
        PluginKey = pluginKey;
    }
}

/// <summary>
/// Thrown when a specific component of a plugin cannot be found.
/// </summary>
public class PluginComponentNotFoundException : ConfigurationException {
    public string ComponentKey { get; }

    public PluginComponentNotFoundException(string componentKey)
        : base($"Plugin component with key '{componentKey}' was not found.") {
        ComponentKey = componentKey;
    }
}
