using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Application.WorkflowStudio;
using ConvoLab.Domain.Execution.Aggregates;

namespace ConvoLab.Application.Tests.WorkflowStudio;

public sealed class WorkflowStudioServiceTests
{
    [Fact]
    public async Task Published_Workflow_Becomes_Runtime_Artifact()
    {
        var repository = new InMemoryWorkflowRepository();
        var service = new WorkflowStudioService(repository, new FakeUnitOfWork());
        var workflow = await service.CreateAsync(new CreateWorkflowCommand("Claims Intake", "", "Kevin", ["claims"]));
        var graph = ValidGraph();
        var version = await service.CreateVersionAsync(workflow.Id, new CreateWorkflowVersionCommand(
            "1.0.0", "Initial", graph.Nodes, graph.Transitions, workflow.Revision));

        version = (await service.TransitionAsync(version.Id, "submit", Command(version.Revision)))!;
        version = (await service.TransitionAsync(version.Id, "approve", Command(version.Revision)))!;
        version = (await service.TransitionAsync(version.Id, "publish", Command(version.Revision)))!;

        Assert.Equal(WorkflowLifecycleStatus.Published, version.Status);
        var published = await service.ListPublishedAsync();
        Assert.Single(published);
        Assert.Equal("Claims Intake v1.0.0", published[0].DisplayName);
    }

    [Fact]
    public async Task Duplicate_Version_Is_Rejected()
    {
        var repository = new InMemoryWorkflowRepository();
        var service = new WorkflowStudioService(repository, new FakeUnitOfWork());
        var workflow = await service.CreateAsync(new CreateWorkflowCommand("Claims", "", "Kevin", []));
        var graph = ValidGraph();
        await service.CreateVersionAsync(workflow.Id, new CreateWorkflowVersionCommand("1.0.0", "", graph.Nodes, graph.Transitions, workflow.Revision));
        var refreshed = (await service.GetAsync(workflow.Id))!;

        await Assert.ThrowsAsync<ResourceConflictException>(() => service.CreateVersionAsync(
            workflow.Id,
            new CreateWorkflowVersionCommand("1.0.0", "", graph.Nodes, graph.Transitions, refreshed.Revision)));
    }

    private static WorkflowLifecycleCommand Command(long revision) => new("reviewer", "test", revision);

    private static (IReadOnlyList<WorkflowNodeInput> Nodes, IReadOnlyList<WorkflowTransitionInput> Transitions) ValidGraph()
    {
        var start = Guid.NewGuid();
        var end = Guid.NewGuid();
        return (
            [
                new WorkflowNodeInput(start, "Start", WorkflowNodeKind.Start, 0, 0, new Dictionary<string, string>()),
                new WorkflowNodeInput(end, "End", WorkflowNodeKind.End, 200, 0, new Dictionary<string, string>())
            ],
            [new WorkflowTransitionInput(Guid.NewGuid(), start, end, null, null)]);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class InMemoryWorkflowRepository : IWorkflowStudioRepository
    {
        private readonly Dictionary<Guid, WorkflowDefinition> _items = [];
        private readonly List<WorkflowAuditState> _audit = [];

        public Task<IReadOnlyList<WorkflowDefinition>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkflowDefinition>>(_items.Values.ToList());
        public Task<WorkflowDefinition?> GetAsync(Guid workflowId, CancellationToken ct = default)
            => Task.FromResult(_items.GetValueOrDefault(workflowId));
        public Task<WorkflowDefinition?> GetByVersionIdAsync(Guid versionId, CancellationToken ct = default)
            => Task.FromResult(_items.Values.FirstOrDefault(item => item.Versions.Any(version => version.Id == versionId)));
        public Task AddAsync(WorkflowDefinition workflow, CancellationToken ct = default)
        {
            _items.Add(workflow.Id, workflow);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(WorkflowDefinition workflow, long expectedWorkflowRevision, Guid? expectedVersionId = null, long? expectedVersionRevision = null, CancellationToken ct = default)
        {
            _items[workflow.Id] = workflow;
            return Task.CompletedTask;
        }
        public Task AddAuditAsync(WorkflowAuditState entry, CancellationToken ct = default)
        {
            _audit.Add(entry);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<WorkflowAuditState>> ListAuditAsync(Guid workflowId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkflowAuditState>>(_audit.Where(entry => _items[workflowId].Versions.Any(version => version.Id == entry.WorkflowVersionId)).ToList());
    }
}
