using System.Text.RegularExpressions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Shiron.Manila.Utils;

namespace Shiron.Manila.API;

public abstract class ProjectFilter {
	public abstract bool predicate(Project p);

	public static ProjectFilter from(object o) {
		Logger.debug("From " + o.GetType());

		if (o is string) {
			var s = (string) o;
			if (s == "*") return new ProjectFilterAll();
			if (s.StartsWith(":")) return new ProjectFilterName(s);
		}

		if (o is IList<object>) {
			Logger.debug("Array");

			var list = (IList<object>) o;
			var filters = new ProjectFilter[list.Count];
			for (var i = 0; i < list.Count; i++) filters[i] = from(list[i]);
			return new ProjectFilterArray(filters);
		}

		if (o is ScriptObject obj) {
			foreach (var key in obj.PropertyNames) {
				try {
					var value = obj.GetProperty(key);
					Logger.debug($"Property: {key}, Value: {value}, Type: {value?.GetType()}");
				} catch (Exception ex) {
					Logger.debug($"Error accessing property {key}: {ex.Message}");
				}
			}

			string objString = o.ToString();
			if (objString.StartsWith("/") && objString.Contains("/")) {
				int lastSlashIndex = objString.LastIndexOf('/');
				string pattern = objString.Substring(1, lastSlashIndex - 1);
				string flags = lastSlashIndex < objString.Length - 1 ? objString.Substring(lastSlashIndex + 1) : "";

				Logger.debug($"Detected regex pattern: '{pattern}', flags: '{flags}'");
				return new ProjectFilterRegex(new Regex(pattern));
			}

			try {
				dynamic dyn = obj;
				var constructorName = dyn.constructor.name;
				if (constructorName == "RegExp") {
					string pattern = dyn.source;
					string flags = dyn.flags;
					Logger.debug($"Detected RegExp object with pattern: '{pattern}', flags: '{flags}'");
					return new ProjectFilterRegex(new Regex(pattern));
				}
			} catch (Exception ex) {
				Logger.debug($"Error checking constructor: {ex.Message}");
			}
		}

		throw new Exception("Invalid project filter. " + o);
	}
}

public class ProjectFilterName : ProjectFilter {
	private readonly string name;

	public ProjectFilterName(string name) {
		this.name = name;
	}

	public override bool predicate(Project p) {
		return p.getIdentifier() == name;
	}
}

public class ProjectFilterAll : ProjectFilter {
	public override bool predicate(Project p) {
		return true;
	}
}

public class ProjectFilterRegex : ProjectFilter {
	private readonly Regex regex;

	public ProjectFilterRegex(Regex regex) {
		this.regex = regex;
	}

	public override bool predicate(Project p) {
		return regex.IsMatch(p.name);
	}
}

public class ProjectFilterArray : ProjectFilter {
	private readonly ProjectFilter[] filters;

	public ProjectFilterArray(ProjectFilter[] filters) {
		this.filters = filters;
	}

	public override bool predicate(Project p) {
		foreach (var filter in filters) if (!filter.predicate(p)) return false;
		return true;
	}
}
