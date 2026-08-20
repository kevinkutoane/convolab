using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConvoLab.Application.Operations;
using ConvoLab.Application.Operations.Backups;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class LocalFileSystemBackupStore : IBackupStore
{
    private readonly string _backupDirectory;
    private readonly ILogger<LocalFileSystemBackupStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public LocalFileSystemBackupStore(IOptions<OperationalBackupOptions> options, ILogger<LocalFileSystemBackupStore> logger)
    {
        _backupDirectory = options.Value.DirectoryPath ?? Path.Combine(AppContext.BaseDirectory, "data", "backups");
        _logger = logger;
    }

    private string GetBackupFolderPath(string backupId)
    {
        // Path traversal protection on BackupId
        var safeId = Path.GetFileName(backupId);
        return Path.Combine(_backupDirectory, safeId);
    }

    public async Task SaveAsync(string backupId, BackupArtifact manifest, Stream databaseStream, Stream? documentsStream, Stream? dataProtectionStream, CancellationToken cancellationToken = default)
    {
        var targetDir = GetBackupFolderPath(backupId);
        Directory.CreateDirectory(targetDir);

        _logger.LogInformation("Saving backup {BackupId} to {Path}", backupId, targetDir);

        // 1. Save Database
        databaseStream.Position = 0;
        await using (var dbFile = File.Create(Path.Combine(targetDir, "database.dump")))
        {
            await databaseStream.CopyToAsync(dbFile, cancellationToken);
        }

        // 2. Save Documents if provided
        if (documentsStream != null)
        {
            documentsStream.Position = 0;
            await using var docsFile = File.Create(Path.Combine(targetDir, "documents.tar.zst"));
            await documentsStream.CopyToAsync(docsFile, cancellationToken);
        }

        // 3. Save Data Protection if provided
        if (dataProtectionStream != null)
        {
            dataProtectionStream.Position = 0;
            await using var dpFile = File.Create(Path.Combine(targetDir, "dataprotection.tar.zst"));
            await dataProtectionStream.CopyToAsync(dpFile, cancellationToken);
        }

        // 4. Save Manifest
        await using var manifestFile = File.Create(Path.Combine(targetDir, "manifest.json"));
        await JsonSerializer.SerializeAsync(manifestFile, manifest, JsonOptions, cancellationToken);
    }

    public async Task<BackupArtifact?> GetManifestAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(GetBackupFolderPath(backupId), "manifest.json");
        if (!File.Exists(manifestPath)) return null;

        await using var stream = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<BackupArtifact>(stream, JsonOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<BackupArtifact>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_backupDirectory)) return Array.Empty<BackupArtifact>();

        var list = new List<BackupArtifact>();
        var subdirs = Directory.GetDirectories(_backupDirectory);

        foreach (var dir in subdirs)
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    await using var stream = File.OpenRead(manifestPath);
                    var manifest = await JsonSerializer.DeserializeAsync<BackupArtifact>(stream, JsonOptions, cancellationToken);
                    if (manifest != null) list.Add(manifest);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize manifest at {Path}", manifestPath);
                }
            }
        }

        return list.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public Task<Stream?> OpenDatabaseStreamAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(GetBackupFolderPath(backupId), "database.dump");
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task<Stream?> OpenDocumentsStreamAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(GetBackupFolderPath(backupId), "documents.tar.zst");
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task<Stream?> OpenDataProtectionStreamAsync(string backupId, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(GetBackupFolderPath(backupId), "dataprotection.tar.zst");
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }
}
