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

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("pg_restore completed with exit code 0 from {InputPath}", inputPath);
                return true;
            }

            // Strict Fail-Closed Policy:
            // Non-zero is ALWAYS a failure unless EVERY line in stderr matches an explicitly allow-listed benign clean warning.
            if (IsOnlyAllowlistedBenignWarnings(stderr, cleanTarget))
            {
                _logger.LogWarning("pg_restore exited with code {ExitCode} containing only allow-listed benign clean warnings. Stderr: {Stderr}",
                    process.ExitCode, stderr);
                return true;
            }

            _logger.LogError("pg_restore failed closed. ExitCode: {ExitCode}. Standard Error: {Stderr}", process.ExitCode, stderr);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception encountered during pg_restore execution.");
            return false;
        }
    }

    public static bool IsOnlyAllowlistedBenignWarnings(string stderr, bool cleanTarget)
    {
        if (string.IsNullOrWhiteSpace(stderr)) return false;

        // Clean target restoration on an empty database produces benign warnings like:
        // "pg_restore: warning: errors ignored on restore: 2"
        // "pg_restore: while PROCESSING TOC: ... table ... does not exist, skipping"
        var lines = stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return false;

        foreach (var line in lines)
        {
            var isAllowlisted =
                (cleanTarget && line.Contains("while PROCESSING TOC:", StringComparison.OrdinalIgnoreCase)) ||
                (cleanTarget && line.Contains("from TOC entry", StringComparison.OrdinalIgnoreCase)) ||
                (cleanTarget && line.Contains("does not exist, skipping", StringComparison.OrdinalIgnoreCase)) ||
                (cleanTarget && Regex.IsMatch(line, @"errors ignored on restore:\s*\d+", RegexOptions.IgnoreCase)) ||
                line.StartsWith("pg_restore: hint:", StringComparison.OrdinalIgnoreCase);

            if (!isAllowlisted)
            {
                // Any line that isn't an explicit allow-listed benign warning causes the entire restore to fail closed
                return false;
            }
        }

        return true;
    }
}
