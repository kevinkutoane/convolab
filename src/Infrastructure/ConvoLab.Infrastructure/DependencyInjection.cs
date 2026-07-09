using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ConvoLab.Infrastructure.Data;

namespace ConvoLab.Infrastructure;

/// <summary>
/// Extension methods for registering infrastructure services in the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add database context
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            // Use SQLite for development if no connection string is provided
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite("Data Source=convolab.db"));
        }
        else if (connectionString.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // Use SQLite
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString));
        }
        else
        {
            // Use PostgreSQL
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        return services;
    }
}
