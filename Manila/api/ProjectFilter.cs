using System.Text.RegularExpressions;
using Microsoft.ClearScript;
using Shiron.Manila.Logging;

namespace Shiron.Manila.API;

/// <summary>
/// Represents a filter for projects to create global configurations for specific projects.
/// </summary>
public abstract class ProjectFilter {
    public abstract bool Predicate(Project p);

    public static ProjectFilter From(object o) {
        Logger.Debug("From " + o.GetType());

        if (o is string) {
            var s = (string) o;
            if (s == "*") return new ProjectFilterAll();
            if (s.StartsWith(":")) return new ProjectFilterName(s);
        }

        if (o is IList<object> list) {
            Logger.Debug("Array");

            var filters = new ProjectFilter[list.Count];
            for (var i = 0; i < list.Count; i++) filters[i] = From(list[i]);
            return new ProjectFilterArray(filters);
        }

        if (o is ScriptObject obj) {
            foreach (var key in obj.PropertyNames) {
                try {
                    var value = obj.GetProperty(key);
                    Logger.Debug($"Property: {key}, Value: {value}, Type: {value?.GetType()}");
                } catch (Exception ex) {
                    Logger.Debug($"Error accessing property {key}: {ex.Message}");
                }
            }

            string objString = o?.ToString() ?? string.Empty;
            if (objString.StartsWith("/") && objString.Contains("/")) {
                int lastSlashIndex = objString.LastIndexOf('/');
                string pattern = objString.Substring(1, lastSlashIndex - 1);
                string flags = lastSlashIndex < objString.Length - 1 ? objString.Substring(lastSlashIndex + 1) : "";

                Logger.Debug($"Detected regex pattern: '{pattern}', flags: '{flags}'");
                return new ProjectFilterRegex(new Regex(pattern));
            }

            try {
                dynamic dyn = obj;
                var constructorName = dyn.constructor.name;
                if (constructorName == "RegExp") {
                    string pattern = dyn.source;
                    string flags = dyn.flags;
                    Logger.Debug($"Detected RegExp object with pattern: '{pattern}', flags: '{flags}'");
                    return new ProjectFilterRegex(new Regex(pattern));
                }
            } catch (Exception ex) {
                Logger.Debug($"Error checking constructor: {ex.Message}");
            }
        }

        throw new Exception("Invalid project filter. " + o);
    }
}

public class ProjectFilterName : ProjectFilter {
    private readonly string _name;

    public ProjectFilterName(string name) {
        this._name = name;
    }

    public override bool Predicate(Project p) {
        return p.GetIdentifier() == _name;
    }
}

public class ProjectFilterAll : ProjectFilter {
    public override bool Predicate(Project p) {
        return true;
    }
}

public class ProjectFilterRegex : ProjectFilter {
    private readonly Regex _regex;

    public ProjectFilterRegex(Regex regex) {
        this._regex = regex;
    }

    public override bool Predicate(Project p) {
        return _regex.IsMatch(p.Name);
    }
}

public class ProjectFilterArray : ProjectFilter {
    private readonly ProjectFilter[] _filters;

    public ProjectFilterArray(ProjectFilter[] filters) {
        this._filters = filters;
    }

    public override bool Predicate(Project p) {
        foreach (var filter in _filters) if (!filter.Predicate(p)) return false;
        return true;
    }
}
