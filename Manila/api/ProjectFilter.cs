using System.Text.RegularExpressions;
using Microsoft.ClearScript;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Logging;

namespace Shiron.Manila.API;

/// <summary>
/// Base class for filtering projects to create targeted configurations.
/// </summary>
public abstract class ProjectFilter {
    /// <summary>
    /// Determines if a project matches the filter criteria.
    /// </summary>
    /// <param name="p">The project to evaluate.</param>
    /// <returns>True if the project matches the filter.</returns>
    public abstract bool Predicate(Project p);

    /// <summary>
    /// Creates a ProjectFilter from various input types including strings, arrays, and script objects.
    /// </summary>
    /// <param name="o">The input object to convert to a filter.</param>
    /// <returns>A ProjectFilter instance based on the input type.</returns>
    /// <exception cref="ManilaException">Thrown when the input cannot be converted to a valid filter.</exception>
    public static ProjectFilter From(object o) {
        Logger.Debug("From " + o.GetType());

        if (o is string) {
            var s = (string) o;
            if (s == "*") return new ProjectFilterAll();
            return new ProjectFilterName(s);
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

        throw new ManilaException("Invalid project filter. " + o);
    }
}

/// <summary>
/// Filters projects by exact name match.
/// </summary>
public class ProjectFilterName : ProjectFilter {
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance with the specified project name.
    /// </summary>
    /// <param name="name">The exact project name to match.</param>
    public ProjectFilterName(string name) {
        this._name = name;
    }

    public override bool Predicate(Project p) {
        return p.GetIdentifier() == _name;
    }
}

/// <summary>
/// Matches all projects without any filtering.
/// </summary>
public class ProjectFilterAll : ProjectFilter {
    /// <summary>
    /// Always returns true for any project.
    /// </summary>
    /// <param name="p">The project to evaluate.</param>
    /// <returns>Always true.</returns>
    public override bool Predicate(Project p) {
        return true;
    }
}

/// <summary>
/// Filters projects using regular expression pattern matching.
/// </summary>
public class ProjectFilterRegex : ProjectFilter {
    private readonly Regex _regex;

    /// <summary>
    /// Initializes a new instance with the specified regex pattern.
    /// </summary>
    /// <param name="regex">The regex pattern to match against project names.</param>
    public ProjectFilterRegex(Regex regex) {
        this._regex = regex;
    }

    public override bool Predicate(Project p) {
        return _regex.IsMatch(p.Name);
    }
}

/// <summary>
/// Filters projects using a collection of multiple filters with OR logic.
/// </summary>
public class ProjectFilterArray : ProjectFilter {
    private readonly ProjectFilter[] _filters;

    /// <summary>
    /// Initializes a new instance with the specified array of filters.
    /// </summary>
    /// <param name="filters">The collection of filters to apply.</param>
    public ProjectFilterArray(ProjectFilter[] filters) {
        this._filters = filters;
    }

    /// <summary>
    /// Returns true if any of the contained filters matches the project.
    /// </summary>
    /// <param name="p">The project to evaluate.</param>
    /// <returns>True if any filter matches the project.</returns>
    public override bool Predicate(Project p) {
        foreach (var filter in _filters) if (filter.Predicate(p)) return true;
        return false;
    }
}
