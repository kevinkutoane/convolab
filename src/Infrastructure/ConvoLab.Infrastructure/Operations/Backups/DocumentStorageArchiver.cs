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
    }

    public async Task<bool> ArchiveDocumentsAsync(Stream destinationStream, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_storageRoot))
        {
            _logger.LogWarning("Knowledge storage path {Path} does not exist. Nothing to archive.", _storageRoot);
            // We write an empty zip to satisfy the stream contract, indicating 0 documents
            using (var emptyZip = new ZipArchive(destinationStream, ZipArchiveMode.Create, leaveOpen: true))
            {
            }
            return true;
        }

        try
        {
            _logger.LogInformation("Archiving documents from {Path}", _storageRoot);

            // Using ZipArchive to package all files, preserving the relative 'yyyy/MM/file.ext' folder structure
            using (var zip = new ZipArchive(destinationStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var directoryInfo = new DirectoryInfo(_storageRoot);
                var files = directoryInfo.GetFiles("*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Create the relative path that matches the Knowledge document storage keys
                    var relativePath = Path.GetRelativePath(_storageRoot, file.FullName).Replace('\\', '/');

                    // Skip the .probe test files that LocalKnowledgeDocumentStorage creates
                    if (relativePath.StartsWith(".probe-", StringComparison.OrdinalIgnoreCase)) continue;

                    var entry = zip.CreateEntry(relativePath, CompressionLevel.Fastest);
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

            using var zip = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Path traversal protection: Ensure the extracted path stays within the intended root
                var fullDestinationPath = Path.GetFullPath(Path.Combine(_storageRoot, entry.FullName));
                var fullRootPath = Path.GetFullPath(_storageRoot) + Path.DirectorySeparatorChar;

                if (!fullDestinationPath.StartsWith(fullRootPath, StringComparison.Ordinal))
                {
                    _logger.LogError("Path traversal attempt detected in backup archive: {EntryName}", entry.FullName);
                    return false;
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    // It's a directory
                    Directory.CreateDirectory(fullDestinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullDestinationPath)!);
                await using var destinationStream = File.Create(fullDestinationPath);
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
