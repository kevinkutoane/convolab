using ConvoLab.Domain.Knowledge.Aggregates;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Knowledge;

public class KnowledgeConnectorTests
{
    private readonly KnowledgeSourceId _sourceId = KnowledgeSourceId.CreateUnique();

    [Fact]
    public void Register_Should_Initialize_Connector()
    {
        var connector = KnowledgeConnector.Register("SP-Conn", _sourceId, KnowledgeSourceType.SharePoint);

        Assert.Equal(ConnectorStatus.Registered, connector.Status);
        Assert.Equal(HealthStatus.Unknown, connector.Health.Status);
        Assert.False(connector.Capabilities.SupportsChangeFeed); // Minimal by default
    }

    [Fact]
    public void CompleteValidation_Should_Activate_Connector()
    {
        var connector = KnowledgeConnector.Register("SP-Conn", _sourceId, KnowledgeSourceType.SharePoint);

        connector.BeginValidation();
        Assert.Equal(ConnectorStatus.Validating, connector.Status);

        connector.CompleteValidation();
        Assert.Equal(ConnectorStatus.Active, connector.Status);
        Assert.Equal(HealthStatus.Healthy, connector.Health.Status);
    }

    [Fact]
    public void FailSynchronization_Should_Degrade_Then_Fail()
    {
        var policy = SynchronizationPolicy.Create(SynchronizationMode.Manual, maxRetries: 3);
        var connector = KnowledgeConnector.Register("SP-Conn", _sourceId, KnowledgeSourceType.SharePoint, syncPolicy: policy);

        connector.BeginValidation();
        connector.CompleteValidation();

        // 1st failure -> Active but degraded health
        connector.BeginSynchronization();
        connector.FailSynchronization("Timeout");
        Assert.Equal(ConnectorStatus.Active, connector.Status);
        Assert.Equal(HealthStatus.Degraded, connector.Health.Status);

        // 2nd failure -> Active but degraded health
        connector.BeginSynchronization();
        connector.FailSynchronization("Timeout");

        // 3rd failure -> Status degrades
        connector.BeginSynchronization();
        connector.FailSynchronization("Timeout");
        Assert.Equal(ConnectorStatus.Degraded, connector.Status);

        // 4th failure -> Exceeds MaxRetries (3), fails completely
        connector.BeginSynchronization();
        connector.FailSynchronization("Timeout");
        Assert.Equal(ConnectorStatus.Failed, connector.Status);
        Assert.Equal(HealthStatus.Unhealthy, connector.Health.Status);
    }
}
