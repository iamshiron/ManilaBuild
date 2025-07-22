
using Shiron.Manila.API;
using Shiron.Manila.Exceptions;
using Shiron.Manila.Utils;

namespace Shiron.Manila.Registries;

public interface IJobRegistry {
    void RegisterJob(Job job);
    Job? GetJob(string uri);
    bool TryGetJob(string name, out Job? job);
    bool HasJob(string name);

    IEnumerable<Job> Jobs { get; }
    IEnumerable<string> JobKeys { get; }
}

public class JobRegistry : IJobRegistry {
    private readonly Dictionary<string, Job> _jobs = [];

    public void RegisterJob(Job job) {
        var uri = new RegexUtils.JobMatch(job.Component is Project ? ((Project) job.Component).Name : null, job.ArtiafactName, job.Name).Format();

        if (_jobs.ContainsKey(uri)) {
            throw new ArgumentException($"Job with uri '{uri}' is already registered.");
        }
        _jobs[uri] = job;
    }

    public Job? GetJob(string uri) => _jobs.TryGetValue(uri, out var job) ? job : null;

    public bool TryGetJob(string name, out Job? job) => _jobs.TryGetValue(name, out job);

    public bool HasJob(string name) => _jobs.ContainsKey(name);

    public IEnumerable<Job> Jobs => _jobs.Values;
    public IEnumerable<string> JobKeys => _jobs.Keys;
}
