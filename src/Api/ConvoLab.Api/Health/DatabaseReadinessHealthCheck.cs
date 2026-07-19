using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConvoLab.Api.Health;

public sealed class DatabaseReadinessHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    private static readonly (string Name, string ProbeSql)[] RequiredTables =
    [
        ("ConversationSimulations", "SELECT 1 FROM \"ConversationSimulations\" WHERE 1 = 0;"),
        ("KnowledgeCollections", "SELECT 1 FROM \"KnowledgeCollections\" WHERE 1 = 0;"),
        ("KnowledgeDocuments", "SELECT 1 FROM \"KnowledgeDocuments\" WHERE 1 = 0;"),
        ("KnowledgeChunks", "SELECT 1 FROM \"KnowledgeChunks\" WHERE 1 = 0;"),
        ("KnowledgeLifecycle", "SELECT 1 FROM \"KnowledgeLifecycle\" WHERE 1 = 0;"),
        ("Prompts", "SELECT 1 FROM \"Prompts\" WHERE 1 = 0;"),
        ("PromptVersions", "SELECT 1 FROM \"PromptVersions\" WHERE 1 = 0;"),
        ("PromptLifecycle", "SELECT 1 FROM \"PromptLifecycle\" WHERE 1 = 0;"),
        ("Workflows", "SELECT 1 FROM \"Workflows\" WHERE 1 = 0;"),
        ("WorkflowVersions", "SELECT 1 FROM \"WorkflowVersions\" WHERE 1 = 0;"),
        ("WorkflowNodes", "SELECT 1 FROM \"WorkflowNodes\" WHERE 1 = 0;"),
        ("WorkflowTransitions", "SELECT 1 FROM \"WorkflowTransitions\" WHERE 1 = 0;"),
        ("WorkflowAudit", "SELECT 1 FROM \"WorkflowAudit\" WHERE 1 = 0;")
    ];

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("The platform database is unreachable.");

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            if (pending.Length != 0)
            {
                return HealthCheckResult.Degraded(
                    $"Database is reachable but {pending.Length} migration(s) are pending.",
                    data: new Dictionary<string, object> { ["pendingMigrations"] = pending });
            }

            foreach (var (table, probeSql) in RequiredTables)
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync(probeSql, cancellationToken);
                }
                catch (Exception exception)
                {
                    return HealthCheckResult.Unhealthy(
                        $"The required database table '{table}' is unavailable.",
                        exception,
                        new Dictionary<string, object> { ["requiredTable"] = table });
                }
            }

            return HealthCheckResult.Healthy("Database connectivity, migrations, and required schema are ready.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Database readiness check failed.", exception);
        }
    }
}
