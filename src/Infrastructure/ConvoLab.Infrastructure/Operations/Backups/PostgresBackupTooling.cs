using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("DefaultConnection connection string is required.");
        _logger = logger;
    }

    private sealed record PostgresConnectionParameters(
        string Host,
        int Port,
        string Database,
        string Username,
        string? Password,
        string? SslMode);

    private PostgresConnectionParameters ParseConnectionString(string connectionString)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2)
            {
                parameters[kv[0].Trim()] = kv[1].Trim();
            }
        }

        var host = parameters.GetValueOrDefault("Host")
                   ?? parameters.GetValueOrDefault("Server")
                   ?? "localhost";

        var portStr = parameters.GetValueOrDefault("Port");
        var port = int.TryParse(portStr, out var p) ? p : 5432;

        var database = parameters.GetValueOrDefault("Database")
                       ?? parameters.GetValueOrDefault("Initial Catalog")
                       ?? "convolab";

        var username = parameters.GetValueOrDefault("Username")
                       ?? parameters.GetValueOrDefault("User Id")
                       ?? parameters.GetValueOrDefault("UID")
                       ?? "postgres";

        var password = parameters.GetValueOrDefault("Password")
                       ?? parameters.GetValueOrDefault("PWD");

        var sslMode = parameters.GetValueOrDefault("SSL Mode")
                      ?? parameters.GetValueOrDefault("SslMode");

        return new PostgresConnectionParameters(host, port, database, username, password, sslMode);
    }

    public async Task<bool> ExecuteDumpAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var conn = ParseConnectionString(_connectionString);

        var args = new List<string>
        {
            "-Fc",
            "--no-owner",
            "--no-privileges",
            "-h", conn.Host,
            "-p", conn.Port.ToString(),
            "-U", conn.Username,
            "-d", conn.Database,
            "-f", outputPath
        };

        var processInfo = new ProcessStartInfo
        {
            FileName = "pg_dump",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            processInfo.ArgumentList.Add(arg);
        }

        if (!string.IsNullOrEmpty(conn.Password))
        {
            processInfo.EnvironmentVariables["PGPASSWORD"] = conn.Password;
        }

        if (!string.IsNullOrEmpty(conn.SslMode))
        {
            processInfo.EnvironmentVariables["PGSSLMODE"] = conn.SslMode;
        }

        _logger.LogInformation("Executing pg_dump against host={Host} port={Port} database={Database} user={User}",
            conn.Host, conn.Port, conn.Database, conn.Username);

        try
        {
            using var process = Process.Start(processInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start pg_dump process. Ensure PostgreSQL client tools are installed.");
                return false;
            }

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("pg_dump failed with exit code {ExitCode}. Standard Error: {Stderr}", process.ExitCode, stderr);
                return false;
            }

            _logger.LogInformation("pg_dump completed successfully to {OutputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered during pg_dump execution.");
            return false;
        }
    }

    public async Task<bool> ExecuteRestoreAsync(string inputPath, bool cleanTarget, CancellationToken cancellationToken = default)
    {
        var conn = ParseConnectionString(_connectionString);

        var args = new List<string>
        {
            "--no-owner",
            "--no-privileges",
            "-h", conn.Host,
            "-p", conn.Port.ToString(),
            "-U", conn.Username,
            "-d", conn.Database
        };

        if (cleanTarget)
        {
            args.Add("-c");
            args.Add("--if-exists");
        }

        args.Add(inputPath);

        var processInfo = new ProcessStartInfo
        {
            FileName = "pg_restore",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            processInfo.ArgumentList.Add(arg);
        }

        if (!string.IsNullOrEmpty(conn.Password))
        {
            processInfo.EnvironmentVariables["PGPASSWORD"] = conn.Password;
        }

        if (!string.IsNullOrEmpty(conn.SslMode))
        {
            processInfo.EnvironmentVariables["PGSSLMODE"] = conn.SslMode;
        }

        _logger.LogInformation("Executing pg_restore against host={Host} port={Port} database={Database} cleanTarget={Clean}",
            conn.Host, conn.Port, conn.Database, cleanTarget);

        try
        {
            using var process = Process.Start(processInfo);
            if (process == null)
            {
                _logger.LogError("Failed to start pg_restore process. Ensure PostgreSQL client tools are installed.");
                return false;
            }

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;

            // pg_restore returns exit code 1 when there are non-fatal warnings (e.g., table already dropped with -c).
            // We treat non-zero as failure unless stderr contains only known ignorable warnings.
            if (process.ExitCode != 0)
            {
                if (IsFatalPgRestoreError(stderr))
                {
                    _logger.LogError("pg_restore failed with fatal exit code {ExitCode}. Standard Error: {Stderr}", process.ExitCode, stderr);
                    return false;
                }

                _logger.LogWarning("pg_restore completed with non-fatal warnings. ExitCode: {ExitCode}, Details: {Stderr}", process.ExitCode, stderr);
            }

            _logger.LogInformation("pg_restore completed successfully from {InputPath}", inputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered during pg_restore execution.");
            return false;
        }
    }

    private static bool IsFatalPgRestoreError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr)) return false;
        // Check for genuine fatal errors like connection failure, auth failure, syntax errors
        return stderr.Contains("FATAL:", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("could not connect", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("password authentication failed", StringComparison.OrdinalIgnoreCase) ||
               stderr.Contains("database \"", StringComparison.OrdinalIgnoreCase) && stderr.Contains("does not exist", StringComparison.OrdinalIgnoreCase);
    }
}
