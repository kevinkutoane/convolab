using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Data;

public sealed class EfUnitOfWork(ApplicationDbContext db) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "The resource changed after it was loaded. Refresh the current version and retry.");
        }
        catch (DbUpdateException)
        {
            throw new ResourceConflictException(
                "persistence.constraint_conflict",
                "The requested change conflicts with an existing persisted resource.");
        }
    }
}
