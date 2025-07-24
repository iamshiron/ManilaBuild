
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Ext;

public class PluginException(string message, Exception? innerException) : ManilaException(message, innerException) {
    public PluginException(string message) : this(message, null) { }
}

public class PluginLoadException(Type pluginType, string file, Exception? innerException) :
    PluginException($"Failed to create instance of plugin {pluginType} from {file}.", innerException) {

    public readonly Type PluginType = pluginType;
    public readonly string File = file;

    public PluginLoadException(Type pluginType, string file)
        : this(pluginType, file, null) {
    }
}

public class PluginNotFoundException(RegexUtils.PluginMatch match, Exception? innerException) :
    PluginException($"Plugin not found: {match.Group}:{match.Plugin}{(match.Version == null ? "" : "@" + match.Version)}", innerException) {

    public readonly RegexUtils.PluginMatch Match = match;

    public PluginNotFoundException(RegexUtils.PluginMatch match)
        : this(match, null) {
    }
}

public class PluginTypeNotFoundException(Type pluginType, Exception? innerException) :
    PluginException($"Plugin '{pluginType}' not found.", innerException) {

    public readonly Type PluginName = pluginType;

    public PluginTypeNotFoundException(Type pluginType)
        : this(pluginType, null) {
    }
}

public class InvalidPluginURIException(string uri, Exception? innerException = null) :
    PluginException($"Invalid plugin URI: {uri}", innerException) {

    public readonly string URI = uri;

    public InvalidPluginURIException(string uri)
        : this(uri, null) {
    }
}

public class PluginComponentNotFoundException(RegexUtils.PluginComponentMatch match, Exception? innerException) :
    PluginException($"Plugin component '{match.Component}' not found in plugin {match.Group}:{match.Plugin}{(match.Version == null ? "" : "@" + match.Version)}", innerException) {

    public readonly RegexUtils.PluginComponentMatch Match = match;

    public PluginComponentNotFoundException(RegexUtils.PluginComponentMatch match)
        : this(match, null) {
    }
}

public class InvalidPluginComponentURIException(string uri, Exception? innerException = null) :
    PluginException($"Invalid plugin component URI: {uri}", innerException) {

    public readonly string URI = uri;

    public InvalidPluginComponentURIException(string uri)
        : this(uri, null) {
    }
}

public class PluginAPIClassNotFoundException(RegexUtils.PluginApiClassMatch match, Exception? innerException) :
    PluginException($"Plugin API class '{match.ApiClass}' not found in plugin {match.Group}:{match.Plugin}{(match.Version == null ? "" : "@" + match.Version)}", innerException) {

    public readonly RegexUtils.PluginApiClassMatch Match = match;
    public PluginAPIClassNotFoundException(RegexUtils.PluginApiClassMatch match)
        : this(match, null) {
    }
}

public class InvalidPluginAPIClassURIException(string uri, Exception? innerException = null) :
    PluginException($"Invalid plugin API class URI: {uri}", innerException) {

    public readonly string URI = uri;

    public InvalidPluginAPIClassURIException(string uri)
        : this(uri, null) {
    }
}
