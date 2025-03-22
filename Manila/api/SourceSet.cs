using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Shiron.Manila.API;

public class SourceSet {
	public string root { get; private set; }
	public List<string> includes { get; private set; } = new();
	public List<string> excludes { get; private set; } = new();

	public SourceSet(string root) {
		this.root = root;
	}

	public SourceSet include(params string[] globs) {
		includes.AddRange(globs);
		return this;
	}
	public SourceSet exclude(params string[] globs) {
		excludes.AddRange(globs);
		return this;
	}

	public File[] files() {
		var matcher = new Matcher();
		foreach (var include in includes) {
			matcher.AddInclude(include);
		}
		foreach (var exclude in excludes) {
			matcher.AddExclude(exclude);
		}

		var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
		return result.Files.Select(f => new File(f.Path)).ToArray();
	}
}
