namespace ConvoLab.Application.Simulation;

public interface IConversationSimulationService
{
    Task<SimulationOptions> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimulationSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<SimulationConversation?> GetAsync(Guid simulationId, CancellationToken cancellationToken = default);
    Task<SimulationConversation> CreateAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default);
    Task<SimulationConversation?> SendMessageAsync(Guid simulationId, SendSimulationMessageCommand command, CancellationToken cancellationToken = default);
    Task<SimulationConversation?> ReplayAsync(Guid simulationId, ReplaySimulationCommand command, CancellationToken cancellationToken = default);
}
