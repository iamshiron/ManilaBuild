
using System.Collections.Concurrent;
using System.Reflection;
using Shiron.Manila.API;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.Exceptions;
using Shiron.Profiling;
using Shiron.Utils;

namespace Shiron.Manila.Registries;

/// <summary>Thread-safe job registry.</summary>
public class JobRegistry(IProfiler profiler) : IJobRegistry {
    private readonly IProfiler _profiler = profiler;
    private readonly ConcurrentDictionary<string, Job> _jobs = [];
    private readonly Lock _lock = new();

    /// <summary>Register new job.</summary>
    public void RegisterJob(Job job) {
        using (new ProfileScope(_profiler, MethodBase.GetCurrentMethod()!)) {
            lock (_lock) {
                var uri = new RegexUtils.JobMatch(job.Component is Project ? ((Project) job.Component).Name : null, job.ArtifactName, job.Name).Format();

                if (_jobs.ContainsKey(uri)) {
                    throw new ManilaException($"Job with uri '{uri}' is already registered.");
                }
                _jobs[uri] = job;
            }
        }
    }

    /// <summary>Get job by full uri.</summary>
    public Job? GetJob(string uri) => _jobs.TryGetValue(uri, out var job) ? job : null;

    /// <summary>Try get job (bool result).</summary>
    public bool TryGetJob(string name, out Job? job) => _jobs.TryGetValue(name, out job);

    /// <summary>Check existence.</summary>
    public bool HasJob(string name) => _jobs.ContainsKey(name);

    /// <summary>All jobs.</summary>
    public IEnumerable<Job> Jobs => _jobs.Values;
    /// <summary>All job keys.</summary>
    public IEnumerable<string> JobKeys => _jobs.Keys;
}
