using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.Intelligence;
using ConvoLab.Infrastructure.Simulation;
using ConvoLab.Infrastructure.KnowledgeStudio;
using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Application.PromptStudio;
using ConvoLab.Application.Simulation;
using ConvoLab.Infrastructure.PromptStudio;
using ConvoLab.Application.WorkflowStudio;
using ConvoLab.Infrastructure.WorkflowStudio;
using ConvoLab.Domain.Intelligence.Interfaces;
using ConvoLab.Application.IntelligenceStudio;
using ConvoLab.Infrastructure.IntelligenceStudio;
using ConvoLab.Application.EvaluationStudio;
using ConvoLab.Infrastructure.EvaluationStudio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ConvoLab.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var configuredProvider = configuration["Database:Provider"]?.Trim().ToLowerInvariant();
        var useSqlite = configuredProvider switch
        {
            "sqlite" => true,
            "postgres" or "postgresql" or "npgsql" => false,
            null or "" => !IsPostgreSqlConnectionString(connectionString),
            _ => throw new InvalidOperationException(
                $"Unsupported database provider '{configuration["Database:Provider"]}'. " +
                "Use Sqlite or PostgreSql.")
        };

        if (useSqlite)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(string.IsNullOrWhiteSpace(connectionString)
                    ? "Data Source=convolab.db"
                    : connectionString));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "A PostgreSQL connection string is required when Database:Provider is PostgreSql.");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));
        }

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IConversationSimulationStore, EfConversationSimulationStore>();
        services.AddScoped<IKnowledgeStudioRepository, EfKnowledgeStudioRepository>();
        services.AddScoped<IPromptStudioRepository, EfPromptStudioRepository>();
        services.AddScoped<IWorkflowStudioRepository, EfWorkflowStudioRepository>();
        services.AddScoped<IEvaluationScorecardRepository, EfEvaluationScorecardRepository>();
        services.AddSingleton<IKnowledgeChunker, DeterministicKnowledgeChunker>();
        services.AddSingleton<IKeywordKnowledgeRetriever, KeywordKnowledgeRetriever>();
        services.AddSingleton<IKnowledgeDocumentStorage, LocalKnowledgeDocumentStorage>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PdfTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, DocxTextExtractor>();
        services.AddSingleton<IDocumentTextExtractorResolver, DocumentTextExtractorResolver>();
        services.AddSingleton<DeterministicIntelligenceExecutor>();
        services.AddHttpClient("Gemini");
        services.AddSingleton<GeminiIntelligenceExecutor>();
        services.AddSingleton<IIntelligenceExecutor, RoutingIntelligenceExecutor>();
        services.AddSingleton<IIntelligenceStudioConfiguration, EnvironmentIntelligenceStudioConfiguration>();
        services.AddSingleton<IEvaluationStudioConfiguration, EnvironmentEvaluationStudioConfiguration>();

        return services;
    }

    private static bool IsPostgreSqlConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;

        try
        {
            return !string.IsNullOrWhiteSpace(new NpgsqlConnectionStringBuilder(connectionString).Host);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
