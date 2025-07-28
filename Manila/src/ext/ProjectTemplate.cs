
using System.Threading.Tasks;
using Shiron.Manila.Exceptions;

namespace Shiron.Manila.Ext;

public interface ITemplateFile {
    string RelativePath { get; }
    bool Validate(Dictionary<string, object?> properties) => true;
    string[] Create(Dictionary<string, object?> properties);
}

public class LambdaTemplateFileLambdaTemplateFile(
        string relativePath,
        Func<Dictionary<string, object?>, string[]> createContentLambda,
        Func<Dictionary<string, object?>, bool>? validateContentLambda = null
) : ITemplateFile {
    public string RelativePath { get; } = relativePath;
    public Func<Dictionary<string, object?>, string[]> CreateContent { get; } = createContentLambda;
    public Func<Dictionary<string, object?>, bool>? ValidateContent { get; } = validateContentLambda;

    public string[] Create(Dictionary<string, object?> properties) => CreateContent(properties);
    public bool Validate(Dictionary<string, object?> properties) => ValidateContent?.Invoke(properties) ?? true;
}

public class TemplateFileBuilder(string relativePath, Func<Dictionary<string, object?>, string[]> createContent) {
    public string RelativePath { get; } = relativePath;
    public readonly Func<Dictionary<string, object?>, string[]> CreateContent = createContent;
    public Func<Dictionary<string, object?>, bool>? ValidateContent { get; private set; } = null;

    public TemplateFileBuilder WithValidation(Func<Dictionary<string, object?>, bool> validateContent) {
        ValidateContent = validateContent;
        return this;
    }

    public ITemplateFile Build() => new LambdaTemplateFileLambdaTemplateFile(
        RelativePath,
        CreateContent,
        ValidateContent
    );
}

public class ProjectTemplateBuilder(string name, string? description = null) {

    public string Name { get; } = name;
    public string? Description { get; } = description;
    public Dictionary<string, ITemplateFile> Files { get; } = [];

    public ProjectTemplateBuilder WithFile(TemplateFileBuilder builder) {
        return WithFile(builder.Build());
    }
    public ProjectTemplateBuilder WithFile(ITemplateFile file) {
        if (!file.RelativePath.StartsWith('/'))
            throw new ConfigurationException($"Template file path '{file.RelativePath}' must start with a '/'");

        Files[file.RelativePath] = file;
        return this;
    }

    public ProjectTemplate Build() {
        return new ProjectTemplate(Name, Description, Files);
    }
}

public record ProjectTemplate(
        string Name,
        string? Description,
        IReadOnlyDictionary<string, ITemplateFile> Files
);

public static class ProjectCreator {
    public static async Task Create(string root, ProjectTemplate template, Dictionary<string, object?> properties) {
        List<Task> tasks = [];

        foreach (var (_, file) in template.Files) {
            var filePath = Path.Join(root, file.RelativePath.TrimStart('/'));
            var directory = Path.GetDirectoryName(filePath);

            if (directory != null && !Directory.Exists(directory)) _ = Directory.CreateDirectory(directory);

            var fileContent = string.Join(Environment.NewLine, file.Create(properties));
            tasks.Add(File.WriteAllTextAsync(filePath, fileContent));
        }

        await Task.WhenAll(tasks);
    }
}
