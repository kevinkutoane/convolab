using ConvoLab.Domain.Replay.Aggregates;

namespace ConvoLab.Domain.Tests.Replay;

public sealed class ReplayExperimentTests
{
    [Fact]
    public void Experiment_requires_candidate_before_completion()
    {
        var experiment = ReplayExperiment.Create(Guid.NewGuid(), Guid.NewGuid(), "Prompt comparison");

        Assert.Throws<InvalidOperationException>(() => experiment.Complete());

        experiment.AddCandidate(Guid.NewGuid());
        experiment.Complete();
        Assert.Equal(ReplayExperimentStatus.Completed, experiment.Status);
    }

    [Fact]
    public void Completed_experiment_is_immutable()
    {
        var experiment = ReplayExperiment.Create(Guid.NewGuid(), Guid.NewGuid(), "Model comparison");
        experiment.AddCandidate(Guid.NewGuid());
        experiment.Complete();

        Assert.Throws<InvalidOperationException>(() => experiment.AddCandidate(Guid.NewGuid()));
    }
}
