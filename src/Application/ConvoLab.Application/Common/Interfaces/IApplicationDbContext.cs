using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
