using System.Linq;
using System.Text.RegularExpressions;
using Shiron.Manila.API.Exceptions;

namespace Shiron.Manila.API;

/// <summary>
/// Defines a base class for filtering projects to create targeted configurations.
/// </summary>
public abstract class ProjectFilter {
    /// <summary>
    /// When overridden in a derived class, determines if a project matches the filter's criteria.
    /// </summary>
    /// <param name="p">The project to evaluate.</param>
    /// <returns><see langword="true"/> if the project matches the filter; otherwise, <see langword="false"/>.</returns>
    public abstract bool Predicate(Project p);

    /// <summary>
    /// Creates a <see cref="ProjectFilter"/> from various input types.
    /// </summary>
    /// <remarks>
    /// This factory method supports:
    /// <list type="bullet">
    /// <item><c>"*"</c> (string) - Matches all projects.</item>
    /// <item>Any other string - Matches a project by its exact identifier.</item>
    /// <item>An array of filters - Matches if any filter in the array matches.</item>
    /// <item>A JavaScript RegExp object - Matches a project's identifier against the pattern.</item>
    /// </list>
    /// </remarks>
    /// <param name="o">The input object, typically from a script.</param>
    /// <returns>A <see cref="ProjectFilter"/> instance.</returns>
    /// <exception cref="ConfigurationException">Thrown if the input object cannot be converted to a valid filter.</exception>
    public static ProjectFilter From(object o) => o switch {
        null => throw new ConfigurationException("Project filter cannot be null."),
        string s when s == "*" => new ProjectFilterAll(),
        string s => new ProjectFilterName(s),
        IList<object> list => new ProjectFilterArray(list.Select(From).ToArray()),
        Regex obj => CreateFilterFromScriptObject(obj),
        _ => throw new ConfigurationException($"Unsupported filter type: '{o.GetType().Name}'. Must be a string, array, or RegExp object."),
    };

    /// <summary>
    /// Handles the conversion of a <see cref="ScriptObject"/> to a <see cref="ProjectFilterRegex"/>.
    /// </summary>
    private static ProjectFilterRegex CreateFilterFromScriptObject(Regex obj) {
        return new ProjectFilterRegex(obj);
    }
}

/// <summary>
/// A filter that matches a project by its exact identifier.
/// </summary>
public class ProjectFilterName(string name) : ProjectFilter {
    private readonly string _name = name;

    /// <inheritdoc/>
    public override bool Predicate(Project p) {
        return p.GetIdentifier() == _name;
    }
}

/// <summary>
/// A filter that matches all projects.
/// </summary>
public class ProjectFilterAll : ProjectFilter {
    /// <inheritdoc/>
    public override bool Predicate(Project p) {
        return true;
    }
}

/// <summary>
/// A filter that matches a project's identifier against a regular expression.
/// </summary>
public class ProjectFilterRegex(Regex regex) : ProjectFilter {
    private readonly Regex _regex = regex;

    /// <inheritdoc/>
    public override bool Predicate(Project p) {
        return _regex.IsMatch(p.GetIdentifier());
    }
}

/// <summary>
/// A composite filter that matches a project if any of its contained filters match (OR logic).
/// </summary>
public class ProjectFilterArray(ProjectFilter[] filters) : ProjectFilter {
    private readonly ProjectFilter[] _filters = filters;

    /// <summary>
    /// Determines if a project matches any of the filters in the collection.
    /// </summary>
    /// <param name="p">The project to evaluate.</param>
    /// <returns><see langword="true"/> if any filter matches; otherwise, <see langword="false"/>.</returns>
    public override bool Predicate(Project p) {
        return _filters.Any(filter => filter.Predicate(p));
    }
}
