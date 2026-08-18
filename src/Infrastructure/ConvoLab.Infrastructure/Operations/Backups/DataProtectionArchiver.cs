using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class DataProtectionArchiver
{
    private readonly string _keyRingPath;
    private readonly ILogger<DataProtectionArchiver> _logger;

    public DataProtectionArchiver(IConfiguration configuration, ILogger<DataProtectionArchiver> logger)
    {
        // Must match exactly what the DataProtection setup uses in Program.cs
        _keyRingPath = configuration["DataProtection:KeyRingPath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "keys");
        _logger = logger;
    }

    public async Task<bool> ArchiveKeyRingAsync(Stream destinationStream, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_keyRingPath))
        {
            _logger.LogWarning("Data protection key ring path {Path} does not exist. Nothing to archive.", _keyRingPath);
            // Write an empty zip to satisfy the stream contract
            using (var emptyZip = new ZipArchive(destinationStream, ZipArchiveMode.Create, leaveOpen: true))
            {
            }
            return true;
        }

        try
        {
            _logger.LogInformation("Archiving data protection keys from {Path}", _keyRingPath);

            using (var zip = new ZipArchive(destinationStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var directoryInfo = new DirectoryInfo(_keyRingPath);
                // Data Protection keys are typically XML files
                var files = directoryInfo.GetFiles("*.xml", SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var entry = zip.CreateEntry(file.Name, CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await using var fileStream = File.OpenRead(file.FullName);
                    await fileStream.CopyToAsync(entryStream, cancellationToken);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive data protection keys from {Path}", _keyRingPath);
            return false;
        }
    }

    public async Task<bool> RestoreKeyRingAsync(Stream sourceStream, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Restoring data protection keys to {Path}", _keyRingPath);

            Directory.CreateDirectory(_keyRingPath);

            using var zip = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: true);
            foreach (var entry in zip.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Path traversal protection
                var fullDestinationPath = Path.GetFullPath(Path.Combine(_keyRingPath, entry.FullName));
                var fullRootPath = Path.GetFullPath(_keyRingPath) + Path.DirectorySeparatorChar;

                if (!fullDestinationPath.StartsWith(fullRootPath, StringComparison.Ordinal))
                {
                    _logger.LogError("Path traversal attempt detected in data protection archive: {EntryName}", entry.FullName);
                    return false;
                }

                // Data protection only uses top-level XML files, directories are unexpected
                if (string.IsNullOrEmpty(entry.Name)) continue;

                // Only allow .xml files to be restored to prevent dropping arbitrary executables
                if (!entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Skipping non-XML file in data protection archive: {EntryName}", entry.FullName);
                    continue;
                }

                await using var destinationStream = File.Create(fullDestinationPath);
                await using var entryStream = entry.Open();
                await entryStream.CopyToAsync(destinationStream, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore data protection keys to {Path}", _keyRingPath);
            return false;
        }
    }
}
