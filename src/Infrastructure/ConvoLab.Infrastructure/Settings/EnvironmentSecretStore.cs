using ConvoLab.Application.Settings;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Settings;

/// <summary>
/// Resolves secret values from environment variables.
/// Supported reference format: env:VARIABLE_NAME
/// Never returns the secret value to callers outside this class.
/// </summary>
public sealed class EnvironmentSecretStore : ISecretStore
{
    private readonly ILogger<EnvironmentSecretStore> _logger;

    public EnvironmentSecretStore(ILogger<EnvironmentSecretStore> logger)
    {
        _logger = logger;
    }

    /// <summary>Resolves the secret value. Returns null if not found or reference is unsupported.</summary>
    public string? Resolve(string reference)
    {
        try
        {
            var (provider, key) = Domain.Settings.SecretReference.ParseReference(reference);
            return provider switch
            {
                "env" => Environment.GetEnvironmentVariable(key),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve secret reference '{Reference}'.", reference);
            return null;
        }
    }

    /// <summary>Returns true if the secret reference resolves to a non-empty value.</summary>
    public bool Validate(string reference)
    {
        var value = Resolve(reference);
        return !string.IsNullOrWhiteSpace(value);
    }
}
