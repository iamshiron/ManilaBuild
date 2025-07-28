
namespace Shiron.Manila.API.Interfaces;

public interface ITemplateFile {
    string RelativePath { get; }
    bool Validate(Dictionary<string, object?> properties) => true;
    string[] Create(Dictionary<string, object?> properties);
}
