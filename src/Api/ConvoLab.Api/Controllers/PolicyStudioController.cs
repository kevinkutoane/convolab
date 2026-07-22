using ConvoLab.Application.PolicyStudio;
using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/policies")]
public sealed class PolicyStudioController(IPolicyStudioService policies) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<PolicyOverviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await policies.GetOverviewAsync(cancellationToken));

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PolicySummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PolicySummaryDto>>> List(CancellationToken cancellationToken)
        => Ok(await policies.ListPoliciesAsync(cancellationToken));

    [HttpGet("{policyId:guid}")]
    [ProducesResponseType<PolicyDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyDetailDto>> Get(Guid policyId, CancellationToken cancellationToken)
        => Ok(await policies.GetPolicyAsync(policyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<PolicyDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PolicyDetailDto>> Create(
        [FromBody] CreatePolicyCommand command,
        CancellationToken cancellationToken)
    {
        var created = await policies.CreatePolicyAsync(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { policyId = created.Summary.Id }, created);
    }

    [HttpPut("{policyId:guid}")]
    [ProducesResponseType<PolicyDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyDetailDto>> Update(
        Guid policyId,
        [FromBody] UpdatePolicyCommand command,
        CancellationToken cancellationToken)
        => Ok(await policies.UpdatePolicyAsync(policyId, command, cancellationToken));

    [HttpPost("{policyId:guid}/versions")]
    [ProducesResponseType<PolicyDetailDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PolicyDetailDto>> CreateVersion(
        Guid policyId,
        [FromBody] CreatePolicyVersionCommand command,
        CancellationToken cancellationToken)
    {
        var created = await policies.CreateVersionAsync(policyId, command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { policyId = created.Summary.Id }, created);
    }

    [HttpPost("{policyId:guid}/{lifecycleAction}")]
    [ProducesResponseType<PolicyDetailDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyDetailDto>> Transition(
        Guid policyId,
        string lifecycleAction,
        CancellationToken cancellationToken)
        => Ok(await policies.TransitionAsync(policyId, lifecycleAction, cancellationToken));

    [HttpPost("evaluate")]
    [ProducesResponseType<PolicyEvaluationResultDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PolicyEvaluationResultDto>> Evaluate(
        [FromBody] EvaluatePolicyCommand command,
        CancellationToken cancellationToken)
        => Ok(await policies.EvaluateAsync(command, cancellationToken));

    [HttpGet("decisions")]
    [ProducesResponseType<IReadOnlyList<PolicyDecisionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PolicyDecisionDto>>> Decisions(
        [FromQuery] int limit = 250,
        CancellationToken cancellationToken = default)
        => Ok(await policies.ListDecisionsAsync(limit, cancellationToken));
}
