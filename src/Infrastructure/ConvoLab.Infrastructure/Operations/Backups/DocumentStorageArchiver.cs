using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class DocumentStorageArchiver
{
    private readonly string _storageRoot;
    private readonly ILogger<DocumentStorageArchiver> _logger;

    public DocumentStorageArchiver(IConfiguration configuration, ILogger<DocumentStorageArchiver> logger)
    {
        _storageRoot = configuration["Knowledge:StoragePath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "knowledge-documents");
        _logger = logger;
        Directory.CreateDirectory(_storageRoot);
    }

    public async Task<bool> ArchiveDocumentsAsync(Stream destinationStream, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_storageRoot))
        {
            _logger.LogWarning("Knowledge storage path {Path} does not exist. Nothing to archive.", _storageRoot);
            using (var emptyZip = new ZipArchive(destinationStream, ZipArchiveMode.Create, leaveOpen: true))
            {
            }
            return true;
        }

        try
        {
            _logger.LogInformation("Archiving documents from {Path}", _storageRoot);

            using (var zip = new ZipArchive(destinationStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var directoryInfo = new DirectoryInfo(_storageRoot);
                var files = directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Reject symlinks/reparse points during archive creation
                    if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        _logger.LogWarning("Skipping symlink or reparse point during archiving: {File}", file.FullName);
                        continue;
                    }

                    var relativePath = Path.GetRelativePath(_storageRoot, file.FullName).Replace('\\', '/');
                    if (relativePath.StartsWith(".probe-", StringComparison.OrdinalIgnoreCase)) continue;

                    var entry = zip.CreateEntry(relativePath, CompressionLevel.Optimal);
                    await using var entryStream = entry.Open();
                    await using var fileStream = File.OpenRead(file.FullName);
                    await fileStream.CopyToAsync(entryStream, cancellationToken);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive document storage from {Path}", _storageRoot);
            return false;
        }
    }

    public async Task<bool> RestoreDocumentsAsync(Stream sourceStream, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Restoring documents to {Path}", _storageRoot);

            Directory.CreateDirectory(_storageRoot);
            var fullRootPath = Path.GetFullPath(_storageRoot);
            if (!fullRootPath.EndsWith(Path.DirectorySeparatorChar))
            {
                fullRootPath += Path.DirectorySeparatorChar;
            }

            using var zip = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var targetPath = Path.GetFullPath(Path.Combine(_storageRoot, entry.FullName));

                // Strict path traversal protection
                if (!targetPath.StartsWith(fullRootPath, StringComparison.Ordinal))
                {
                    _logger.LogError("Path traversal attempt detected in document archive entry: {EntryName}", entry.FullName);
                    return false;
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(targetPath);
                    continue;
                }

                var parentDir = Path.GetDirectoryName(targetPath)!;
                Directory.CreateDirectory(parentDir);

                // Verify parent directory is not a symlink/reparse point
                var parentInfo = new DirectoryInfo(parentDir);
                if ((parentInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    _logger.LogError("Refusing to extract into reparse point / symlink directory: {Directory}", parentDir);
                    return false;
                }

                await using var destinationStream = File.Create(targetPath);
                await using var entryStream = entry.Open();
                await entryStream.CopyToAsync(destinationStream, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore document storage to {Path}", _storageRoot);
            return false;
        }
    }
}
