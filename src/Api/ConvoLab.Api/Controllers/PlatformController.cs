using Microsoft.AspNetCore.Mvc;

namespace ConvoLab.Api.Controllers;

[ApiController]
[Route("api/platform")]
public sealed class PlatformController : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<PlatformStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<PlatformStatusResponse> GetStatus()
    {
        var response = new PlatformStatusResponse(
            PlatformName: "ConvoLab Platform",
            ProductName: "ConvoLab Studio",
            Version: "1.0.0-alpha.5",
            Environment: HttpContext.RequestServices
                .GetRequiredService<IHostEnvironment>()
                .EnvironmentName,
            ArchitectureHealth: "Hardened Alpha",
            ApiHealth: "Healthy",
            Capabilities:
            [
                new("conversation", "Conversation Engine", "Lifecycle, sessions, participants, memory, and timeline.", "stable", "1.0", 16),
                new("workflow", "Workflow Engine", "Versioned workflow definitions and governed executions.", "stable", "1.0", 12),
                new("prompt", "Prompt Engine", "Governed prompt assets, versions, rendering, and experiments.", "stable", "1.0", 10),
                new("knowledge", "Knowledge Engine", "Governed retrieval, packages, citations, and connectors.", "stable", "1.0", 13),
                new("intelligence", "Intelligence Engine", "Provider-neutral planning, budgets, tools, and fallback.", "stable", "1.0", 14),
                new("policy", "Policy", "Central governance and runtime decision constraints.", "foundation", "0.5", 4),
                new("evaluation", "Evaluation", "Persisted scorecards, quality gates, safety, relevance, and groundedness telemetry.", "stable", "1.0", 5),
                new("tracing", "Tracing", "Distributed trace model, spans, events, and artifacts.", "foundation", "0.5", 7),
                new("plugins", "Plugin Engine", "Extensible capabilities, lifecycle, health, and metadata.", "foundation", "0.5", 4),
                new("studio", "ConvoLab Studio", "Functional engineering workspace with conversation simulation, workflow design, prompt governance, knowledge ingestion, trace inspection, and replay.", "active", "0.3", 0),
            ],
            GeneratedAt: DateTimeOffset.UtcNow,
            Source: "api");

        return Ok(response);
    }
}

public sealed record PlatformStatusResponse(
    string PlatformName,
    string ProductName,
    string Version,
    string Environment,
    string ArchitectureHealth,
    string ApiHealth,
    IReadOnlyList<PlatformCapabilityResponse> Capabilities,
    DateTimeOffset GeneratedAt,
    string Source);

public sealed record PlatformCapabilityResponse(
    string Id,
    string Name,
    string Description,
    string Status,
    string Version,
    int DomainEvents);
