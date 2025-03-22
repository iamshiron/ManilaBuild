namespace Shiron.Manila.API;

using System.Dynamic;
using System.Reflection;
using Microsoft.ClearScript;
using Shiron.Manila.Attributes;
using Shiron.Manila.Utils;

public class Project : Component {
	[ScriptProperty(true)]
	public string name { get; private set; }

	[ScriptProperty]
	public string? version { get; set; }
	[ScriptProperty]
	public string? group { get; set; }
	[ScriptProperty]
	public string? description { get; set; }

	public Dictionary<string, SourceSet> _sourceSets = new();

	[ScriptFunction]
	public void sourceSets(object obj) {
		foreach (var pair in (IDictionary<string, object>) obj) {
			if (_sourceSets.ContainsKey(pair.Key)) throw new Exception($"SourceSet '{pair.Key}' already exists.");
			_sourceSets.Add(pair.Key, (SourceSet) pair.Value);
		}
	}

	public Project(string name, string location) : base(location) {
		this.name = name;
	}
}
