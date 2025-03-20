namespace Shiron.Manila.API;

public class Dir {
	public string path { get; private set; }

	public Dir(string path) {
		this.path = path;
	}

	public File[] files() {
		string[] files = Directory.GetFiles(this.path);
		File[] result = new File[files.Length];
		for (int i = 0; i < files.Length; i++) {
			result[i] = new File(files[i]);
		}
		return result;
	}

	public File file(string name) {
		return new File(Path.Combine(this.path, name));
	}

	public Dir join(params object[] path) {
		var newPath = this.path;
		foreach (var p in path) {
			newPath = Path.Combine(newPath, p.ToString());
		}
		return new Dir(newPath);
	}
	public Dir join(Dir dir) {
		return new Dir(Path.Combine(this.path, dir.path));
	}

	public bool isAbsolute() {
		return Path.IsPathRooted(this.path);
	}
	public bool exists() {
		return Directory.Exists(this.path);
	}
	public void create() {
		Directory.CreateDirectory(this.path);
	}

	public string get() {
		return this.path;
	}

	public static implicit operator string(Dir d) {
		return d.path;
	}
}
