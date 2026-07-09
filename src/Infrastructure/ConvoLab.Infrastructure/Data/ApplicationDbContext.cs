using Microsoft.EntityFrameworkCore;
using ConvoLab.Domain.Entities;

namespace ConvoLab.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the ConvoLab application.
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Configures the database model.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch domain events before saving
        await DispatchDomainEventsAsync();

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Dispatches domain events from entities.
    /// </summary>
    private async Task DispatchDomainEventsAsync()
    {
        // This is a placeholder for domain event dispatching
        // In a production application, you would publish these events to a message broker
        // or an in-process event dispatcher

        var entities = ChangeTracker
            .Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .ToList();

        foreach (var entry in entities)
        {
            entry.Entity.ClearDomainEvents();
        }

        await Task.CompletedTask;
    }
}
