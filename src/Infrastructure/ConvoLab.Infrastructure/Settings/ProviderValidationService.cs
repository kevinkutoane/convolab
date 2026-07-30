using System.Diagnostics;
using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConvoLab.Infrastructure.Settings;

/// <summary>
/// Validates the effective AI provider configuration for an environment by
/// resolving the secret reference and probing the provider's model metadata
/// endpoint (a cost-free call that consumes no tokens). Outcomes are recorded
/// in the configuration change log; secret values never leave this class.
/// </summary>
public sealed class ProviderValidationService : IProviderValidationService
{
    private readonly ApplicationDbContext _db;
    private readonly IEffectiveConfigurationResolver _resolver;
    private readonly ISecretStore _secretStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProviderValidationService> _logger;

    public ProviderValidationService(
        ApplicationDbContext db,
        IEffectiveConfigurationResolver resolver,
        ISecretStore secretStore,
        IHttpClientFactory httpClientFactory,
        ILogger<ProviderValidationService> logger)
    {
        _db = db; _resolver = resolver; _secretStore = secretStore;
        _httpClientFactory = httpClientFactory; _logger = logger;
    }

    public async Task<ProviderValidationResultDto> ValidateAsync(
        Guid workspaceId, Guid environmentId, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct = default)
    {
        var ws = await _db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workspaceId, ct)
            ?? throw new ResourceNotFoundException("workspace.not_found", $"Workspace '{workspaceId}' was not found.");
        _ = await _db.RuntimeEnvironments.AsNoTracking().SingleOrDefaultAsync(e => e.Id == environmentId && e.WorkspaceId == workspaceId, ct)
            ?? throw new ResourceNotFoundException("environment.not_found", $"Environment '{environmentId}' was not found.");

        var stopwatch = Stopwatch.StartNew();
        var effective = await _resolver.ResolveAsync(ws.OrganisationId, workspaceId, environmentId, ct);
        string? Get(string key) => effective.FirstOrDefault(r => r.Key == key)?.EffectiveValue?.Trim('"');

        var provider = Get(SettingKeys.AiProvider) ?? "Gemini";
        var model = Get(SettingKeys.AiModel) ?? "gemini-2.5-flash";
        var enabled = bool.TryParse(Get(SettingKeys.AiProviderEnabled), out var e2) && e2;
        var timeoutSeconds = int.TryParse(Get(SettingKeys.AiRequestTimeoutSeconds), out var t) ? Math.Clamp(t, 5, 300) : 30;
        var secretReference = Get(SettingKeys.AiSecretReference);

        var result = await ValidateCoreAsync(provider, model, enabled, timeoutSeconds, secretReference, stopwatch, ct);
        await RecordAuditAsync(ws.OrganisationId, workspaceId, environmentId, result, actorId, actorDisplay, correlationId, ct);
        return result;
    }

    private async Task<ProviderValidationResultDto> ValidateCoreAsync(
        string provider, string model, bool enabled, int timeoutSeconds, string? secretReference,
        Stopwatch stopwatch, CancellationToken ct)
    {
        ProviderValidationResultDto Fail(string outcome, string message, bool secretResolved = false, bool reachable = false, bool auth = false, bool modelAvailable = false) =>
            new(outcome, message, secretResolved, reachable, auth, modelAvailable, (int)stopwatch.ElapsedMilliseconds);

        if (!enabled)
            return Fail("ProviderDisabled", "The AI provider is disabled for this environment (ai.provider_enabled = false).");

        if (!provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            return Fail("ConfigurationInvalid", $"Provider '{provider}' is not supported by this platform build. Supported: Gemini.");

        // ─── Resolve the secret without ever exposing it ─────────────────────
        string? apiKey = null;
        if (!string.IsNullOrWhiteSpace(secretReference))
        {
            try { apiKey = _secretStore.Resolve(secretReference); }
            catch { /* treated as unresolved below */ }
        }
        if (string.IsNullOrWhiteSpace(apiKey))
            return Fail("SecretMissing",
                string.IsNullOrWhiteSpace(secretReference)
                    ? "No persisted secret reference is configured (ai.secret_reference)."
                    : $"The secret reference '{secretReference}' did not resolve to a value.");

        // ─── Probe the provider: model metadata endpoint, zero token cost ────
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}?key={Uri.EscapeDataString(apiKey)}";
        var httpClient = _httpClientFactory.CreateClient("Gemini");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            using var response = await httpClient.GetAsync(endpoint, cts.Token);
            stopwatch.Stop();

            return (int)response.StatusCode switch
            {
                200 => new ProviderValidationResultDto("Valid",
                    $"Provider configuration is valid. Model '{model}' is reachable and the credential is accepted.",
                    true, true, true, true, (int)stopwatch.ElapsedMilliseconds),
                400 or 401 or 403 => Fail("InvalidCredentials",
                    "The provider rejected the credential. Verify the API key referenced by ai.secret_reference.",
                    secretResolved: true, reachable: true),
                404 => Fail("ModelUnavailable",
                    $"The credential is accepted but model '{model}' was not found. Check ai.model.",
                    secretResolved: true, reachable: true, auth: true),
                429 => Fail("RateLimited",
                    "The provider is rate limiting requests. The credential appears valid; retry shortly.",
                    secretResolved: true, reachable: true, auth: true),
                _ => Fail("ProviderUnavailable",
                    $"The provider returned an unexpected status ({(int)response.StatusCode}).",
                    secretResolved: true, reachable: true)
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            return Fail("TimedOut", $"The provider did not respond within {timeoutSeconds}s.", secretResolved: true);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "Provider validation could not reach the provider.");
            return Fail("ProviderUnavailable", "The provider endpoint could not be reached. Check network egress from the API host.", secretResolved: true);
        }
    }

    private async Task RecordAuditAsync(
        Guid organisationId, Guid workspaceId, Guid environmentId,
        ProviderValidationResultDto result, Guid actorId, string actorDisplay, string correlationId, CancellationToken ct)
    {
        var change = new ConfigurationChangeRecord
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisationId,
            WorkspaceId = workspaceId,
            EnvironmentId = environmentId,
            SettingKey = "ai.provider_validation",
            PreviousValueSummary = null,
            NewValueSummary = $"{result.Outcome} in {result.DurationMs}ms",
            ChangedBy = actorId,
            ChangedByDisplay = actorDisplay,
            ChangedAt = DateTimeOffset.UtcNow,
            Reason = result.Message.Length > 900 ? result.Message[..900] : result.Message,
            CorrelationId = correlationId,
            Outcome = result.Outcome == "Valid" ? "Succeeded" : "Failed",
            Revision = 1
        };
        _db.ConfigurationChanges.Add(change);
        await AnalyticsOutboxFactory.EnqueueConfigurationChangeAsync(_db, change, ct);
        await _db.SaveChangesAsync(ct);
    }
}
