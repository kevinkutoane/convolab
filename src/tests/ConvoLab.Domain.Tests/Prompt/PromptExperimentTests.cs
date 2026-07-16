using ConvoLab.Domain.Prompt.Aggregates;
using ConvoLab.Domain.Prompt.Entities;
using ConvoLab.Domain.Prompt.Enums;
using ConvoLab.Domain.Prompt.Events;
using ConvoLab.Domain.Prompt.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Prompt;

public class PromptExperimentTests
{
    private readonly Guid _userId = Guid.NewGuid();

    private PromptExperiment CreateExperiment()
        => PromptExperiment.Create("A/B Test: Greeting", "Testing formal vs casual greetings", _userId);

    [Fact]
    public void Create_Should_Initialize_With_Pending_Status()
    {
        var experiment = CreateExperiment();

        Assert.Equal(ExperimentStatus.Pending, experiment.Status);
        Assert.Empty(experiment.Variants);
    }

    [Fact]
    public void Start_Without_Variants_Should_Throw()
    {
        var experiment = CreateExperiment();

        Assert.Throws<InvalidOperationException>(() => experiment.Start());
    }

    [Fact]
    public void Start_With_Variants_Should_Succeed()
    {
        var experiment = CreateExperiment();
        var variant = PromptVariant.Create("Control", PromptVersionId.CreateUnique(), 50);
        experiment.AddVariant(variant);

        experiment.Start();

        Assert.Equal(ExperimentStatus.Running, experiment.Status);
        Assert.NotNull(experiment.StartedAt);
    }

    [Fact]
    public void Start_Should_Raise_ExperimentStartedEvent()
    {
        var experiment = CreateExperiment();
        experiment.AddVariant(PromptVariant.Create("Control", PromptVersionId.CreateUnique(), 100));

        experiment.Start();

        Assert.Contains(experiment.DomainEvents, e => e is ExperimentStartedEvent);
    }

    [Fact]
    public void Complete_Should_Raise_ExperimentCompletedEvent()
    {
        var experiment = CreateExperiment();
        experiment.AddVariant(PromptVariant.Create("Control", PromptVersionId.CreateUnique(), 100));
        experiment.Start();
        experiment.ClearDomainEvents();

        experiment.Complete(new[] { "eval-ref-001" });

        Assert.Equal(ExperimentStatus.Completed, experiment.Status);
        Assert.Contains(experiment.DomainEvents, e => e is ExperimentCompletedEvent);
        Assert.Single(experiment.EvaluationReferences);
    }

    [Fact]
    public void Complete_Non_Running_Experiment_Should_Throw()
    {
        var experiment = CreateExperiment();

        Assert.Throws<InvalidOperationException>(() => experiment.Complete());
    }

    [Fact]
    public void Cancel_Completed_Experiment_Should_Throw()
    {
        var experiment = CreateExperiment();
        experiment.AddVariant(PromptVariant.Create("Control", PromptVersionId.CreateUnique(), 100));
        experiment.Start();
        experiment.Complete();

        Assert.Throws<InvalidOperationException>(() => experiment.Cancel());
    }

    [Fact]
    public void AddVariant_To_Running_Experiment_Should_Throw()
    {
        var experiment = CreateExperiment();
        experiment.AddVariant(PromptVariant.Create("Control", PromptVersionId.CreateUnique(), 100));
        experiment.Start();

        Assert.Throws<InvalidOperationException>(() =>
            experiment.AddVariant(PromptVariant.Create("Treatment", PromptVersionId.CreateUnique(), 50)));
    }
}
