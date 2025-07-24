
namespace Shiron.Manila.Exceptions;

public class URIException(string uri, string message, Exception? innerException = null) : ManilaException(message, innerException) {
    public readonly string URI = uri;

    public URIException(string uri, string message) : this(uri, message, null) { }
}
