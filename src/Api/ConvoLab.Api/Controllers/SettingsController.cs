using System.Security.Claims;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.WorkspaceIdentity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/environments")]
public sealed class EnvironmentsController(IEnvironmentService service) : ControllerBase
{
    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EnvironmentDto>>> List(Guid workspaceId, CancellationToken ct)
        => Ok(await service.ListAsync(workspaceId, ct));

    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet("{environmentId:guid}")]
    public async Task<ActionResult<EnvironmentDto>> Get(Guid workspaceId, Guid environmentId, CancellationToken ct)
        => Ok(await service.GetAsync(workspaceId, environmentId, ct));

    [Authorize(Policy = WorkspacePermissions.ManageEnvironmentSettings)]
    [HttpPost]
    public async Task<ActionResult<EnvironmentDto>> Create(Guid workspaceId, CreateEnvironmentRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync(workspaceId, request, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return CreatedAtAction(nameof(Get), new { workspaceId, environmentId = dto.Id }, dto);
    }

    [Authorize(Policy = WorkspacePermissions.ManageEnvironmentSettings)]
    [HttpPatch("{environmentId:guid}")]
    public async Task<ActionResult<EnvironmentDto>> Update(Guid workspaceId, Guid environmentId, UpdateEnvironmentRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(workspaceId, environmentId, request, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    [Authorize(Policy = WorkspacePermissions.ManageEnvironmentSettings)]
    [HttpPost("{environmentId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid workspaceId, Guid environmentId, [FromBody] RevisionRequest request, CancellationToken ct)
    {
        await service.ActivateAsync(workspaceId, environmentId, request.ExpectedRevision, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [Authorize(Policy = WorkspacePermissions.ManageEnvironmentSettings)]
    [HttpPost("{environmentId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid workspaceId, Guid environmentId, [FromBody] RevisionRequest request, CancellationToken ct)
    {
        var isAdmin = User.HasClaim("role", "Administrator");
        await service.SuspendAsync(workspaceId, environmentId, request.ExpectedRevision, isAdmin, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [Authorize(Policy = WorkspacePermissions.ManageEnvironmentSettings)]
    [HttpPost("{environmentId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid workspaceId, Guid environmentId, [FromBody] RevisionRequest request, CancellationToken ct)
    {
        await service.ArchiveAsync(workspaceId, environmentId, request.ExpectedRevision, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [Authorize(Policy = WorkspacePermissions.ManageEnvironmentSettings)]
    [HttpPost("{environmentId:guid}/make-default")]
    public async Task<IActionResult> MakeDefault(Guid workspaceId, Guid environmentId, [FromBody] RevisionRequest request, CancellationToken ct)
    {
        await service.MakeDefaultAsync(workspaceId, environmentId, request.ExpectedRevision, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpPost("{environmentId:guid}/select")]
    public async Task<ActionResult<EnvironmentDto>> Select(Guid workspaceId, Guid environmentId, CancellationToken ct)
        => Ok(await service.SelectAsync(
            workspaceId,
            environmentId,
            ActorId(),
            User.FindFirstValue("actor_type") ?? "User",
            User.FindFirstValue(ClaimTypes.Role),
            HttpContext.TraceIdentifier,
            ct));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private string ActorDisplay() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email) ?? "Authenticated user";
}

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/settings")]
public sealed class WorkspaceSettingsController(ISettingsService service) : ControllerBase
{
    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingValueDto>>> List(Guid workspaceId, CancellationToken ct)
        => Ok(await service.ListWorkspaceSettingsAsync(workspaceId, ct));

    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet("effective")]
    public async Task<ActionResult<IReadOnlyList<EffectiveSettingDto>>> Effective(Guid workspaceId, [FromQuery] Guid? environmentId, CancellationToken ct)
        => Ok(await service.GetEffectiveWorkspaceSettingsAsync(workspaceId, environmentId, ct));

    [Authorize(Policy = WorkspacePermissions.ManageWorkspaceSettings)]
    [HttpPatch("{settingKey}")]
    public async Task<ActionResult<SettingValueDto>> Upsert(Guid workspaceId, string settingKey, UpsertSettingRequest request, CancellationToken ct)
        => Ok(await service.UpsertWorkspaceSettingAsync(workspaceId, settingKey, request, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    [Authorize(Policy = WorkspacePermissions.ManageWorkspaceSettings)]
    [HttpDelete("{settingKey}")]
    public async Task<IActionResult> Delete(Guid workspaceId, string settingKey, CancellationToken ct)
    {
        await service.DeleteWorkspaceSettingAsync(workspaceId, settingKey, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet("changes")]
    public async Task<ActionResult<IReadOnlyList<ConfigurationChangeDto>>> Changes(Guid workspaceId, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await service.GetChangeHistoryAsync(workspaceId, null, take, ct));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private string ActorDisplay() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email) ?? "Authenticated user";
}

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/environments/{environmentId:guid}/settings")]
public sealed class EnvironmentSettingsController(ISettingsService service, IProviderValidationService providerValidation) : ControllerBase
{
    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingValueDto>>> List(Guid workspaceId, Guid environmentId, CancellationToken ct)
        => Ok(await service.ListEnvironmentSettingsAsync(workspaceId, environmentId, ct));

    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet("effective")]
    public async Task<ActionResult<IReadOnlyList<EffectiveSettingDto>>> Effective(Guid workspaceId, Guid environmentId, CancellationToken ct)
        => Ok(await service.GetEffectiveEnvironmentSettingsAsync(workspaceId, environmentId, ct));

    [Authorize(Policy = WorkspacePermissions.ManageEnvironmentSettings)]
    [HttpPatch("{settingKey}")]
    public async Task<ActionResult<SettingValueDto>> Upsert(Guid workspaceId, Guid environmentId, string settingKey, UpsertSettingRequest request, CancellationToken ct)
        => Ok(await service.UpsertEnvironmentSettingAsync(workspaceId, environmentId, settingKey, request, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    [Authorize(Policy = WorkspacePermissions.ManageEnvironmentSettings)]
    [HttpDelete("{settingKey}")]
    public async Task<IActionResult> Delete(Guid workspaceId, Guid environmentId, string settingKey, CancellationToken ct)
    {
        await service.DeleteEnvironmentSettingAsync(workspaceId, environmentId, settingKey, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet("changes")]
    public async Task<ActionResult<IReadOnlyList<ConfigurationChangeDto>>> Changes(Guid workspaceId, Guid environmentId, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await service.GetChangeHistoryAsync(workspaceId, environmentId, take, ct));

    [Authorize(Policy = WorkspacePermissions.ExportConfiguration)]
    [HttpGet("export")]
    public async Task<ActionResult<ConfigurationExportDto>> Export(Guid workspaceId, Guid environmentId, CancellationToken ct)
        => Ok(await service.ExportAsync(workspaceId, environmentId, ct));

    [Authorize(Policy = WorkspacePermissions.ImportConfiguration)]
    [HttpPost("import")]
    public async Task<ActionResult<IReadOnlyList<ConfigurationChangeDto>>> Import(Guid workspaceId, Guid environmentId, ImportConfigurationRequest request, CancellationToken ct)
        => Ok(await service.ImportAsync(workspaceId, environmentId, request, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    /// <summary>Validates every effective setting for the environment against its definition (types, ranges, enums, cross-field rules).</summary>
    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpPost("validate")]
    public async Task<ActionResult<SettingsValidationResultDto>> Validate(Guid workspaceId, Guid environmentId, CancellationToken ct)
        => Ok(await service.ValidateEnvironmentSettingsAsync(workspaceId, environmentId, ct));

    /// <summary>Performs a live, cost-free check of the effective AI provider configuration (secret resolution, reachability, credential, model availability).</summary>
    [Authorize(Policy = WorkspacePermissions.ValidateProviderConfiguration)]
    [HttpPost("provider/validate")]
    public async Task<ActionResult<ProviderValidationResultDto>> ValidateProvider(Guid workspaceId, Guid environmentId, CancellationToken ct)
        => Ok(await providerValidation.ValidateAsync(workspaceId, environmentId, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private string ActorDisplay() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email) ?? "Authenticated user";
}

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/secret-references")]
public sealed class SecretReferencesController(ISecretReferenceService service) : ControllerBase
{
    [Authorize(Policy = WorkspacePermissions.ManageSecretReferences)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SecretReferenceDto>>> List(Guid workspaceId, CancellationToken ct)
        => Ok(await service.ListAsync(workspaceId, ct));

    [Authorize(Policy = WorkspacePermissions.ManageSecretReferences)]
    [HttpPost]
    public async Task<ActionResult<SecretReferenceDto>> Create(Guid workspaceId, CreateSecretReferenceRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync(workspaceId, request, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return CreatedAtAction(nameof(List), new { workspaceId }, dto);
    }

    [Authorize(Policy = WorkspacePermissions.ManageSecretReferences)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SecretReferenceDto>> Get(Guid workspaceId, Guid id, CancellationToken ct)
        => Ok(await service.GetAsync(workspaceId, id, ct));

    [Authorize(Policy = WorkspacePermissions.ManageSecretReferences)]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<SecretReferenceDto>> Update(Guid workspaceId, Guid id, UpdateSecretReferenceRequest request, CancellationToken ct)
        => Ok(await service.UpdateAsync(workspaceId, id, request, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    [Authorize(Policy = WorkspacePermissions.ManageSecretReferences)]
    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<SecretReferenceDto>> Validate(Guid workspaceId, Guid id, CancellationToken ct)
        => Ok(await service.ValidateAsync(workspaceId, id, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    [Authorize(Policy = WorkspacePermissions.ManageSecretReferences)]
    [HttpPost("{id:guid}/disable")]
    public async Task<ActionResult<SecretReferenceDto>> Disable(Guid workspaceId, Guid id, [FromBody] RevisionRequest request, CancellationToken ct)
        => Ok(await service.DisableAsync(workspaceId, id, request.ExpectedRevision, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private string ActorDisplay() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email) ?? "Authenticated user";
}

[ApiController]
[Route("api/organisations/{organisationId:guid}/settings")]
public sealed class OrganisationSettingsController(ISettingsService service) : ControllerBase
{
    [Authorize(Policy = WorkspacePermissions.ViewSettings)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingValueDto>>> List(Guid organisationId, CancellationToken ct)
        => Ok(await service.ListOrganisationSettingsAsync(organisationId, ct));

    [Authorize(Policy = "PlatformAdministrator")]
    [HttpPatch("{settingKey}")]
    public async Task<ActionResult<SettingValueDto>> Upsert(Guid organisationId, string settingKey, UpsertSettingRequest request, CancellationToken ct)
        => Ok(await service.UpsertOrganisationSettingAsync(organisationId, settingKey, request, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct));

    [Authorize(Policy = "PlatformAdministrator")]
    [HttpDelete("{settingKey}")]
    public async Task<IActionResult> Delete(Guid organisationId, string settingKey, CancellationToken ct)
    {
        await service.DeleteOrganisationSettingAsync(organisationId, settingKey, ActorId(), ActorDisplay(), HttpContext.TraceIdentifier, ct);
        return NoContent();
    }

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private string ActorDisplay() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Email) ?? "Authenticated user";
}
