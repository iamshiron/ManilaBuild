using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Shiron.Manila.API;
using Shiron.Manila.API.Exceptions;
using Shiron.Manila.API.Interfaces;
using Shiron.Manila.API.Interfaces.Artifacts;
using Shiron.Manila.Interfaces;
using Shiron.Manila.Logging;
using Shiron.Manila.Utils;
using Spectre.Console;

namespace Shiron.Manila.Caching;

public class RemoteArtifactCache(string host, string key, ILogger logger, IDirectories directories, IArtifactCache localCache) : IArtifactCache {
    private readonly ILogger _logger = logger;
    private readonly IArtifactCache _local = localCache;
    private readonly IDirectories _directories = directories;
    private readonly string _host = host;
    private readonly string _key = key;

    private HttpClient? _http;

    // Delegated lifecycle
    public void LoadCache() {
        _logger.Debug("LoadCache delegating to local cache.");
        _local.LoadCache();
    }
    public void FlushCacheToDisk() {
        _logger.Debug("FlushCacheToDisk delegating to local cache.");
        _local.FlushCacheToDisk();
    }

    // Delegated queries
    public bool IsCached(string fingerprint) {
        var cached = _local.IsCached(fingerprint);
        _logger.Debug($"IsCached('{fingerprint}') => {cached} (local).");
        return cached;
    }
    public ArtifactOutput GetMostRecentOutputForProject(Project project) => _local.GetMostRecentOutputForProject(project);

    // Delegated utility
    public void UpdateCacheAccessTime(BuildExitCodeCached cachedExitCode) {
        _logger.Debug($"UpdateCacheAccessTime for '{cachedExitCode.CacheKey}'.");
        _local.UpdateCacheAccessTime(cachedExitCode);
    }

    public async Task<ICreatedArtifact> AppendCachedDataAsync(ICreatedArtifact artifact, BuildConfig config, Project project) {
        _logger.Debug($"AppendCachedDataAsync for artifact '{artifact.Name}' (project '{project.Name}').");
        return await _local.AppendCachedDataAsync(artifact, config, project);
    }

    public async Task CacheArtifactAsync(ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output) {
        _logger.Debug($"CacheArtifactAsync for artifact '{artifact.Name}' (project '{project.Name}').");
        await _local.CacheArtifactAsync(artifact, config, project, output);
        await TryPushToRemoteAsync(artifact, config, project, output);
    }

    private HttpClient? EnsureHttpClient() {
        if (_http != null) return _http;
        try {
            var baseUri = _host.EndsWith('/') ? _host : _host + "/";
            _http = new HttpClient { BaseAddress = new Uri(baseUri) };
            if (!string.IsNullOrWhiteSpace(_key)) {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _key);
            }

            _logger.Debug($"HTTP client created. Base='{_http.BaseAddress}', Auth={(string.IsNullOrWhiteSpace(_key) ? "none" : "bearer")}.");
            return _http;
        } catch (Exception e) {
            _logger.Warning($"Failed to create HTTP client for cache server at '{_host}': {e.Message}");
            return null;
        }
    }

    public async Task<bool> CheckAvailability() {
        var http = EnsureHttpClient();
        if (http == null) return false;

        try {
            _logger.Debug("Checking remote cache availability via GET /ping ...");
            var resp = await http.GetAsync("ping");
            if (resp.IsSuccessStatusCode) {
                _logger.Debug("Remote cache is available.");
                return true;
            } else {
                _logger.Warning($"Remote cache ping failed ({(int) resp.StatusCode}): {resp.ReasonPhrase}");
                throw new ManilaException($"Remote cache is not available ({(int) resp.StatusCode}): {resp.ReasonPhrase}");
            }
        } catch (Exception e) {
            var ex = new ManilaException($"Remote cache availability check failed with exception: {e.Message}", e);
            _logger.Warning(ex.Message);
            throw ex;
        }
    }

    private async Task TryPushToRemoteAsync(ICreatedArtifact artifact, BuildConfig config, Project project, ArtifactOutput output) {
        var http = EnsureHttpClient();
        if (http == null) return;

        var fingerprint = artifact.GetFingerprint(project, config);
        _logger.Debug($"Begin remote push for fingerprint '{fingerprint}'.");

        // 1) Register artifact metadata
        try {
            var meta = new {
                name = artifact.Name,
                project = project.Name,
                type = artifact.PluginComponent.Format(),
            };
            using var metaContent = new StringContent(JsonSerializer.Serialize(meta), Encoding.UTF8, "application/json");
            _logger.Debug($"PUT /artifacts/{fingerprint} (name='{meta.name}', project='{meta.project}', type='{meta.type}').");
            using var putResp = await http.PutAsync($"artifacts/{Uri.EscapeDataString(fingerprint)}", metaContent);
            if (!putResp.IsSuccessStatusCode) {
                _logger.Warning($"Remote cache PUT failed ({(int) putResp.StatusCode}): {putResp.ReasonPhrase}");
                return; // don't attempt upload if registration failed
            } else {
                _logger.Debug("PUT succeeded.");
            }
        } catch (Exception e) {
            _logger.Warning($"Remote cache PUT failed with exception: {e.Message}");
            return;
        }

        // 2) Zip and upload outputs
        using (var stream = new MemoryStream()) {
            var added = 0;
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                foreach (var filePath in output.FilePaths) {
                    if (!File.Exists(filePath))
                        throw new ManilaException($"Cannot add file '{filePath}' to remote cache zip: file does not exist.");

                    var entryPath = Path.GetRelativePath(output.ArtifactRoot, filePath);
                    _ = archive.CreateEntryFromFile(filePath, entryPath);
                    added++;
                }
            }

            stream.Position = 0;

            _logger.Debug($"Prepared zip archive for upload. Size={stream.Length} bytes, Entries={added}.");
            var split = fingerprint.Split('_');
            var hash = split[split.Length - 1];

            _logger.Debug($"Uploading artifact data to /artifacts/{hash} ...");
            var content = new ByteArrayContent(stream.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            var res = await http.PostAsync($"artifacts/{fingerprint}/output", new MultipartFormDataContent {
                { content, "\"file\"", "artifact.zip" }
            });

            if (!res.IsSuccessStatusCode) {
                _logger.Warning($"Remote cache artifact upload failed ({(int) res.StatusCode}): {res.ReasonPhrase}");
            } else {
                _logger.Info($"Successfully pushed artifact '{artifact.Name}' (fingerprint '{fingerprint}') to remote cache.");
            }
        }
    }
}
