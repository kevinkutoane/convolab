using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.IntelligenceStudio;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.WorkspaceIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/intelligence/providers")]
public sealed class IntelligenceProvidersController : ControllerBase
{
    private readonly IIntelligenceStudioConfiguration _studioConfiguration;
    private readonly IProviderValidationService _providerValidation;
    private readonly IRuntimeRequestContext _runtime;

    public IntelligenceProvidersController(
        IIntelligenceStudioConfiguration studioConfiguration,
        IProviderValidationService providerValidation,
        IRuntimeRequestContext runtime)
    {
        _studioConfiguration = studioConfiguration;
        _providerValidation = providerValidation;
        _runtime = runtime;
    }

    [HttpGet]
    [Authorize(Policy = WorkspacePermissions.WorkspaceMember)]
    public ActionResult GetProviders()
        => Ok(_studioConfiguration.GetProviders());

    [HttpPost("{provider}/test")]
    [Authorize(Policy = WorkspacePermissions.ValidateProviderConfiguration)]
    public async Task<ActionResult> Test(string provider, CancellationToken cancellationToken)
    {
        if (provider.Equals("Deterministic", StringComparison.OrdinalIgnoreCase))
            return Ok(new { provider = "Deterministic", status = "Ready", latencyMs = 0, outcome = "Valid", message = "Local test provider is operational." });
        if (!provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            throw new ResourceNotFoundException("provider.not_found", $"Provider '{provider}' was not found.");

        if (_runtime.WorkspaceId is not Guid workspaceId)
            throw new RequestValidationException("workspace.required", "An active workspace is required to test provider configuration.");

        var environmentId = _runtime.EnvironmentId;
        if (environmentId is not Guid resolvedEnvironmentId)
            throw new RequestValidationException(
                "runtime_environment.required",
                "Select a runtime environment before testing a provider.");

        var result = await _providerValidation.ValidateAsync(
            workspaceId,
            resolvedEnvironmentId,
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
