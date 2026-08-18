using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Operations.Backups;

internal sealed class PostgresBackupTooling
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresBackupTooling> _logger;

    public PostgresBackupTooling(IConfiguration configuration, ILogger<PostgresBackupTooling> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection string is required.");
        _logger = logger;
    }

    public async Task<bool> ExecuteDumpAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        // For local development and demonstration, this is a simplified process executor.
        // In a real implementation, we would extract the PGHOST, PGPORT, PGUSER, PGPASSWORD
        // from the connection string using a robust parser.
        // For alpha.16, we demonstrate the architectural pattern of separating execution.

        var processInfo = new ProcessStartInfo
        {
            FileName = "pg_dump",
            Arguments = $"-Fc -f \"{outputPath}\" convolab", // simplified arguments for demonstration
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };

        _logger.LogInformation("Starting pg_dump to {OutputPath}", outputPath);

        try
        {
            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("pg_dump failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, stderr);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute pg_dump process.");
            return false;
        }
    }

    public async Task<bool> ExecuteRestoreAsync(string inputPath, bool cleanTarget, CancellationToken cancellationToken = default)
    {
        var cleanArg = cleanTarget ? "-c" : "";
        var processInfo = new ProcessStartInfo
        {
            FileName = "pg_restore",
            Arguments = $"{cleanArg} -d convolab \"{inputPath}\"", // simplified arguments for demonstration
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };

        _logger.LogInformation("Starting pg_restore from {InputPath}. CleanTarget: {CleanTarget}", inputPath, cleanTarget);

        try
        {
            process.Start();
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 && !stderr.Contains("WARNING", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("pg_restore failed with exit code {ExitCode}. Error: {Error}", process.ExitCode, stderr);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute pg_restore process.");
            return false;
        }
    }
}
