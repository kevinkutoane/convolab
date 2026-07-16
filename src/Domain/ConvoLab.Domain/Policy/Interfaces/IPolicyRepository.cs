using ConvoLab.Domain.Policy.Aggregates;
using ConvoLab.Domain.Policy.Enums;
using ConvoLab.Domain.Policy.ValueObjects;

namespace ConvoLab.Domain.Policy.Interfaces;

/// <summary>Repository for policy definitions.</summary>
public interface IPolicyRepository
{
    Task<PolicyDefinition?> GetByIdAsync(PolicyDefinitionId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PolicyDefinition>> GetActiveByDomainAsync(PolicyDomain domain, Guid? tenantId = null, CancellationToken cancellationToken = default);
    Task AddAsync(PolicyDefinition policy, CancellationToken cancellationToken = default);
    Task UpdateAsync(PolicyDefinition policy, CancellationToken cancellationToken = default);
}
