namespace ConvoLab.Application.IntelligenceStudio;

public interface IIntelligenceCatalogueBootstrapper
{
    Task EnsureReadyAsync(CancellationToken cancellationToken = default);
}
