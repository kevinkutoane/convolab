using ConvoLab.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ConvoLab.Infrastructure.IntegrationTests.Data;

public sealed class DatabaseProviderSelectionTests
{
    [Theory]
    [InlineData(null, null, "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("Data Source=convolab.db", null, "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("Host=localhost;Database=convolab;Username=postgres;Password=postgres", null, "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData("Data Source=convolab.db", "PostgreSql", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void AddInfrastructure_Selects_The_Expected_Database_Provider(
        string? connectionString,
        string? configuredProvider,
        string expectedProvider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Database:Provider"] = configuredProvider
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(expectedProvider, db.Database.ProviderName);
    }

    [Fact]
    public void AddInfrastructure_Rejects_An_Unknown_Explicit_Provider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Oracle"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains("Unsupported database provider", exception.Message);
    }
}
