using ConvoLab.Application.KnowledgeStudio;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed class DocumentStorageHealthCheck(IKnowledgeDocumentStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => await storage.ProbeAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Knowledge document storage is writable.")
            : HealthCheckResult.Unhealthy("Knowledge document storage is not writable.");
}
