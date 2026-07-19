using ConvoLab.Domain.Execution.Aggregates;

namespace ConvoLab.Domain.Tests.Workflow;

public sealed class WorkflowDefinitionTests
{
    [Fact]
    public void Linear_Graph_Is_Valid()
    {
        var workflow = new WorkflowDefinition(Guid.NewGuid(), "Claims", "Claims workflow");
        var version = workflow.CreateVersion(1, 0, 0);
        var start = version.AddNode(Guid.NewGuid(), "Start", WorkflowNodeKind.Start, 0, 0);
        var prompt = version.AddNode(Guid.NewGuid(), "Prompt", WorkflowNodeKind.Prompt, 200, 0);
        var end = version.AddNode(Guid.NewGuid(), "End", WorkflowNodeKind.End, 400, 0);
        version.AddTransition(Guid.NewGuid(), start.Id, prompt.Id);
        version.AddTransition(Guid.NewGuid(), prompt.Id, end.Id);

        Assert.Empty(version.ValidateGraph());
    }

    [Fact]
    public void Decision_Requires_Two_Outgoing_Branches()
    {
        var workflow = new WorkflowDefinition(Guid.NewGuid(), "Claims", "Claims workflow");
        var version = workflow.CreateVersion(1, 0, 0);
        var start = version.AddNode(Guid.NewGuid(), "Start", WorkflowNodeKind.Start, 0, 0);
        var decision = version.AddNode(Guid.NewGuid(), "Covered?", WorkflowNodeKind.Decision, 200, 0);
        var end = version.AddNode(Guid.NewGuid(), "End", WorkflowNodeKind.End, 400, 0);
        version.AddTransition(Guid.NewGuid(), start.Id, decision.Id);
        version.AddTransition(Guid.NewGuid(), decision.Id, end.Id);

        Assert.Contains(version.ValidateGraph(), issue => issue.Code == "workflow.decision.branches");
    }

    [Fact]
    public void Published_Version_Is_Immutable()
    {
        var (_, version) = ValidWorkflow();
        version.Submit("designer");
        version.Approve("reviewer");
        version.Publish("publisher");

        Assert.Throws<InvalidOperationException>(() =>
            version.AddNode(Guid.NewGuid(), "Late node", WorkflowNodeKind.Prompt, 600, 0));
    }

    [Fact]
    public void Submit_Rejects_Invalid_Graph()
    {
        var workflow = new WorkflowDefinition(Guid.NewGuid(), "Invalid", "Invalid workflow");
        var version = workflow.CreateVersion(1, 0, 0);
        version.AddNode(Guid.NewGuid(), "Only prompt", WorkflowNodeKind.Prompt, 0, 0);

        Assert.Throws<InvalidOperationException>(() => version.Submit("designer"));
    }

    [Fact]
    public void Publishing_New_Version_Deprecates_Previous_Published_Version()
    {
        var workflow = new WorkflowDefinition(Guid.NewGuid(), "Claims", "Claims workflow");
        var first = AddValidVersion(workflow, 1, 0, 0);
        first.Submit("designer");
        first.Approve("reviewer");
        workflow.PublishVersion(first.Id, "publisher");

        var second = AddValidVersion(workflow, 1, 1, 0);
        second.Submit("designer");
        second.Approve("reviewer");
        workflow.PublishVersion(second.Id, "publisher");

        Assert.Equal(WorkflowLifecycleStatus.Deprecated, first.Status);
        Assert.Equal(WorkflowLifecycleStatus.Published, second.Status);
    }

    private static (WorkflowDefinition Workflow, WorkflowVersion Version) ValidWorkflow()
    {
        var workflow = new WorkflowDefinition(Guid.NewGuid(), "Claims", "Claims workflow");
        return (workflow, AddValidVersion(workflow, 1, 0, 0));
    }

    private static WorkflowVersion AddValidVersion(WorkflowDefinition workflow, int major, int minor, int patch)
    {
        var version = workflow.CreateVersion(major, minor, patch);
        var start = version.AddNode(Guid.NewGuid(), "Start", WorkflowNodeKind.Start, 0, 0);
        var end = version.AddNode(Guid.NewGuid(), "End", WorkflowNodeKind.End, 200, 0);
        version.AddTransition(Guid.NewGuid(), start.Id, end.Id);
        return version;
    }
}
