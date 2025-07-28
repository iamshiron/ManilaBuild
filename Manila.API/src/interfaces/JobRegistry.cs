
namespace Shiron.Manila.API.Interfaces;

public interface IJobRegistry {
    void RegisterJob(Job job);
    Job? GetJob(string uri);
    bool TryGetJob(string name, out Job? job);
    bool HasJob(string name);

    IEnumerable<Job> Jobs { get; }
    IEnumerable<string> JobKeys { get; }
}
