namespace Shiron.Manila.Exceptions;

/// <summary>
/// Represents errors related to user configuration, such as invalid settings
/// in a Manila.js file, incorrect project structure, or missing plugins.
/// </summary>
public class ConfigurationException : ConfigurationTimeException {
    public ConfigurationException(string message) : base(message) { }

    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}
