using System.Text.RegularExpressions;
using ConvoLab.Application.KnowledgeStudio;
using Microsoft.Extensions.Configuration;

namespace ConvoLab.Infrastructure.KnowledgeStudio;

public sealed class LocalKnowledgeDocumentStorage : IKnowledgeDocumentStorage
{
    private readonly string _root;
    public LocalKnowledgeDocumentStorage(IConfiguration configuration)
    {
        _root = configuration["Knowledge:StoragePath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "knowledge-documents");
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredKnowledgeDocument> StoreAsync(string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var stem = Regex.Replace(Path.GetFileNameWithoutExtension(fileName), "[^a-zA-Z0-9._-]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(stem)) stem = "document";
        var safe = $"{stem[..Math.Min(stem.Length, 80)]}{extension}";
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
        var full = FullPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var output = File.Create(full);
        await content.CopyToAsync(output, ct);
        return new(key, safe, contentType, output.Length);
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default) => Task.FromResult<Stream>(File.OpenRead(FullPath(storageKey)));
    public Task DeleteAsync(string storageKey, CancellationToken ct = default) { var p=FullPath(storageKey); if(File.Exists(p)) File.Delete(p); return Task.CompletedTask; }
    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default) => Task.FromResult(File.Exists(FullPath(storageKey)));

    public async Task<bool> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(_root);
            var probe = Path.Combine(_root, $".probe-{Guid.NewGuid():N}");
            await File.WriteAllTextAsync(probe, "ok", ct);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string FullPath(string key)
    {
        var candidate = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException("Invalid storage key.");
        return candidate;
    }
}
