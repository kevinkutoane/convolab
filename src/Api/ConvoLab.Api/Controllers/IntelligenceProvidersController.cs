using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.IntelligenceStudio;
using ConvoLab.Application.Settings;
using ConvoLab.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/intelligence/providers")]
public sealed class IntelligenceProvidersController : ControllerBase
{
    private readonly IIntelligenceStudioConfiguration _studioConfiguration;
    private readonly IProviderValidationService _providerValidation;
    private readonly IRuntimeRequestContext _runtime;
    private readonly ApplicationDbContext _db;

    public IntelligenceProvidersController(
        IIntelligenceStudioConfiguration studioConfiguration,
        IProviderValidationService providerValidation,
        IRuntimeRequestContext runtime,
        ApplicationDbContext db)
    {
        _studioConfiguration = studioConfiguration;
        _providerValidation = providerValidation;
        _runtime = runtime;
        _db = db;
    }

    [HttpGet]
    public ActionResult GetProviders()
        => Ok(_studioConfiguration.GetProviders());

    [HttpPost("{provider}/test")]
    public async Task<ActionResult> Test(string provider, CancellationToken cancellationToken)
    {
        if (provider.Equals("Deterministic", StringComparison.OrdinalIgnoreCase))
            return Ok(new { provider = "Deterministic", status = "Ready", latencyMs = 0, outcome = "Valid", message = "Local test provider is operational." });
        if (!provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            throw new ResourceNotFoundException("provider.not_found", $"Provider '{provider}' was not found.");

        var workspaceId = _runtime.WorkspaceId;
        if (!workspaceId.HasValue)
        {
            workspaceId = await _db.Workspaces.AsNoTracking()
                .Where(w => w.Status == "Active")
                .Select(w => (Guid?)w.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!workspaceId.HasValue)
            throw new RequestValidationException("workspace.required", "An active workspace is required to test provider configuration.");

        var environmentId = _runtime.EnvironmentId;
        if (!environmentId.HasValue)
        {
            environmentId = await _db.RuntimeEnvironments.AsNoTracking()
                .Where(e => e.WorkspaceId == workspaceId.Value && e.IsDefault && e.Status == "Active")
                .Select(e => (Guid?)e.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!environmentId.HasValue)
        {
            environmentId = await _db.RuntimeEnvironments.AsNoTracking()
                .Where(e => e.WorkspaceId == workspaceId.Value && e.Status == "Active")
                .Select(e => (Guid?)e.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!environmentId.HasValue)
            throw new RequestValidationException(
                "runtime_environment.required",
                "Select a runtime environment before testing a provider.");

        var result = await _providerValidation.ValidateAsync(
            workspaceId.Value,
            environmentId.Value,
            _runtime.ActorId ?? Guid.Empty,
            _runtime.ActorType,
            _runtime.CorrelationId,
            providerOverride: provider,
            ct: cancellationToken);

        return Ok(new
        {
            provider = provider,
            status = result.Outcome == "Valid" ? "Ready" : result.Outcome,
            latencyMs = result.DurationMs,
            outcome = result.Outcome,
            message = result.Message,
            authSucceeded = result.AuthSucceeded,
            providerReachable = result.ProviderReachable,
            secretResolved = result.SecretResolved
        });
    }
}
