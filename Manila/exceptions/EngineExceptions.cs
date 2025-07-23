namespace Shiron.Manila.Exceptions;

public class UnableToInitializeEngineException : ManilaException {
    public UnableToInitializeEngineException(string message) : base(message) { }

    public UnableToInitializeEngineException(string message, Exception innerException) : base(message, innerException) { }
}
