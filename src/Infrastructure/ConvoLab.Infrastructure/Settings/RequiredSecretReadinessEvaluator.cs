using System.Text.Json;
using ConvoLab.Application.Operations;
using ConvoLab.Application.Settings;
using ConvoLab.Domain.Settings;
using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ConvoLab.Infrastructure.Settings;

public sealed class RequiredSecretReadinessEvaluator(
    ApplicationDbContext db,
    IEffectiveConfigurationResolver configurationResolver,
    ISecretStore secretStore,
    IHostEnvironment hostEnvironment,
    IOptions<RequiredSecretReadinessOptions> options) : IRequiredSecretReadinessEvaluator
{
    public async Task<RequiredSecretReadinessSnapshot> EvaluateAsync(
        CancellationToken ct = default)
    {
        var candidates = await db.RuntimeEnvironments.AsNoTracking()
            .Where(environment => environment.Status == "Active")
            .Join(
                db.Workspaces.AsNoTracking().Where(workspace =>
                    workspace.Status == "Active"),
                environment => environment.WorkspaceId,
                workspace => workspace.Id,
                (environment, workspace) => new { Environment = environment, Workspace = workspace })
            .Join(
                db.Organisations.AsNoTracking().Where(organisation =>
                    organisation.Status == "Active"),
                item => item.Workspace.OrganisationId,
                organisation => organisation.Id,
                (item, organisation) => new
                {
                    item.Environment,
                    WorkspaceId = item.Workspace.Id,
                    OrganisationId = organisation.Id
                })
            .ToListAsync(ct);

        var selectors = Selectors();
        var scoped = candidates.Where(item => InScope(
            item.Environment.Id,
            item.Environment.Name,
            item.Environment.EnvironmentType,
            item.Environment.IsDefault,
            selectors)).ToList();
        var failures = selectors.Length == 0
            ? []
            : selectors.Where(selector => !scoped.Any(item => Matches(
                    selector,
                    item.Environment.Id,
                    item.Environment.Name)))
                .Select(_ => "required_secrets.environment_not_found_or_inactive")
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        var evidence = new List<RequiredSecretEvidence>(scoped.Count);
        foreach (var item in scoped)
        {
            var effective = await configurationResolver.ResolveAsync(
                item.OrganisationId,
                item.WorkspaceId,
                item.Environment.Id,
                ct);
            string? Value(string key) => ReadScalar(
                effective.FirstOrDefault(setting => setting.Key == key)?.EffectiveValue);
            bool Flag(string key, bool fallback) =>
                bool.TryParse(Value(key), out var parsed) ? parsed : fallback;

            var provider = Value(SettingKeys.AiProvider) ?? "NotConfigured";
            var executionEnabled = Flag(SettingKeys.FeatureProviderExecution, true)
                                   && Flag(SettingKeys.AiProviderEnabled, true);
            var external = !options.Value.ProvidersWithoutSecrets.Contains(
                provider,
                StringComparer.OrdinalIgnoreCase);
            var required = executionEnabled && external && provider != "NotConfigured";
            if (!required)
            {
                evidence.Add(new(
                    item.Environment.Id,
                    item.Environment.Name,
                    provider,
                    null,
                    false,
                    provider == "NotConfigured"
                        ? OperationalDependencyState.NotConfigured
                        : OperationalDependencyState.Configured,
                    null,
                    null));
                continue;
            }

            var reference = Value(SettingKeys.AiSecretReference);
            if (string.IsNullOrWhiteSpace(reference))
            {
                evidence.Add(new(
                    item.Environment.Id,
                    item.Environment.Name,
                    provider,
                    null,
                    true,
                    OperationalDependencyState.Unavailable,
                    "secret.required_not_configured",
                    null));
                continue;
            }

            string scheme;
            try
            {
                (scheme, _) = SecretReference.ParseReference(reference);
            }
            catch (ArgumentException)
            {
                evidence.Add(new(
                    item.Environment.Id,
                    item.Environment.Name,
                    provider,
                    null,
                    true,
                    OperationalDependencyState.Unavailable,
                    "secret.reference.invalid",
                    DateTimeOffset.UtcNow));
                continue;
            }

            var validation = await secretStore.ValidateAsync(reference, ct);
            evidence.Add(new(
                item.Environment.Id,
                item.Environment.Name,
                provider,
                scheme,
                true,
                validation.DependencyState,
                validation.ErrorCode,
                DateTimeOffset.UtcNow));
        }

        return new(evidence, failures);
    }

    private string[] Selectors() => hostEnvironment.IsProduction()
        ? options.Value.ProductionEnvironmentIdsOrNames
        : hostEnvironment.IsEnvironment("UAT")
            ? options.Value.UatEnvironmentIdsOrNames
            : [];

    private bool InScope(
        Guid id,
        string name,
        string environmentType,
        bool isDefault,
        string[] selectors)
    {
        if (hostEnvironment.IsDevelopment())
            return isDefault
                   && environmentType.Equals("Development", StringComparison.OrdinalIgnoreCase);
        if (hostEnvironment.IsProduction())
            return environmentType.Equals("Production", StringComparison.OrdinalIgnoreCase)
                   && selectors.Any(selector => Matches(selector, id, name));
        if (hostEnvironment.IsEnvironment("UAT"))
            return environmentType.Equals("UAT", StringComparison.OrdinalIgnoreCase)
                   && selectors.Any(selector => Matches(selector, id, name));
        return isDefault;
    }

    private static bool Matches(string selector, Guid id, string name) =>
        selector.Equals(id.ToString(), StringComparison.OrdinalIgnoreCase)
        || selector.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static string? ReadScalar(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.String
                ? document.RootElement.GetString()
                : document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return value.Trim();
        }
    }
}
