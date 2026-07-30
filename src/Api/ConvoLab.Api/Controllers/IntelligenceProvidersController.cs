using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.IntelligenceStudio;
using ConvoLab.Application.Settings;
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
    public ActionResult GetProviders()
        => Ok(_studioConfiguration.GetProviders());

    [HttpPost("{provider}/test")]
    public async Task<ActionResult> Test(string provider, CancellationToken cancellationToken)
    {
        if (provider.Equals("Deterministic", StringComparison.OrdinalIgnoreCase))
            return Ok(new { provider = "Deterministic", status = "Ready", latencyMs = 0 });
        if (!provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            throw new ResourceNotFoundException("provider.not_found", $"Provider '{provider}' was not found.");

        if (_runtime.WorkspaceId is not Guid workspaceId
            || _runtime.EnvironmentId is not Guid environmentId)
            throw new RequestValidationException(
                "runtime_environment.required",
                "Select a runtime environment before testing a provider.");

        var result = await _providerValidation.ValidateAsync(
            workspaceId,
            environmentId,
            _runtime.ActorId ?? Guid.Empty,
            _runtime.ActorType,
            _runtime.CorrelationId,
            cancellationToken);
        return Ok(result);
    }
}
