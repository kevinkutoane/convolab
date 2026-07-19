namespace ConvoLab.Application.Simulation;

public interface IConversationSimulationStore
{
    Task<IReadOnlyList<SimulationState>> ListAsync(CancellationToken cancellationToken = default);
    Task<SimulationState?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SimulationState> AddAsync(CreateSimulationCommand command, CancellationToken cancellationToken = default);
    Task SaveAsync(SimulationState state, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
