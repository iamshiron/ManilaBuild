namespace Shiron.Manila.Exceptions;

public class UnableToInitializeEngineException : ConfigurationTimeException {
    public UnableToInitializeEngineException(string message) : base(message) { }

    public UnableToInitializeEngineException(string message, Exception innerException) : base(message, innerException) { }
}
