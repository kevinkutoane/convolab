using System.Reflection;
using ConvoLab.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.Services;
namespace ConvoLab.Application;
public static class DependencyInjection {
    public static IServiceCollection AddApplication(this IServiceCollection services) {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });
        services.AddTransient<IConversationEngine, PlaceholderConversationEngine>();
        services.AddTransient<IPromptEngine, PlaceholderPromptEngine>();
        // Capability 5 — Knowledge Engine: repositories, retriever, governance policy, and the engine.
        services.AddSingleton<ConvoLab.Domain.Knowledge.Interfaces.IKnowledgeSourceRepository, InMemoryKnowledgeSourceRepository>();
        services.AddSingleton<ConvoLab.Domain.Knowledge.Interfaces.IKnowledgeCollectionRepository, InMemoryKnowledgeCollectionRepository>();
        services.AddSingleton<ConvoLab.Domain.Knowledge.Interfaces.IKnowledgeConnectorRepository, InMemoryKnowledgeConnectorRepository>();
        services.AddSingleton<ConvoLab.Domain.Knowledge.Interfaces.IKnowledgeRetriever, PlaceholderKnowledgeRetriever>();
        services.AddSingleton<ConvoLab.Domain.Knowledge.Policies.KnowledgeGovernancePolicy>();
        services.AddTransient<IKnowledgeEngine, KnowledgeEngine>();
        services.AddTransient<IAIOrchestrator, PlaceholderAIOrchestrator>(); // Legacy shim; superseded by IIntelligenceEngine.
        // Capability 6 — Intelligence Engine: catalogue, executions, budgets, planner, executor port, and the engine.
        services.AddSingleton<ConvoLab.Domain.Intelligence.Interfaces.IIntelligenceProviderRepository, InMemoryIntelligenceProviderRepository>();
        services.AddSingleton<ConvoLab.Domain.Intelligence.Interfaces.IExecutionRequestRepository, InMemoryExecutionRequestRepository>();
        services.AddSingleton<ConvoLab.Domain.Intelligence.Interfaces.IExecutionBudgetRepository, InMemoryExecutionBudgetRepository>();
        services.AddSingleton<ConvoLab.Domain.Intelligence.Interfaces.IIntelligenceExecutor, PlaceholderIntelligenceExecutor>();
        services.AddSingleton<ConvoLab.Domain.Intelligence.Services.ExecutionPlanner>();
        services.AddTransient<IIntelligenceEngine, IntelligenceEngine>();
        services.AddTransient<ITraceEngine, PlaceholderTraceEngine>();
        services.AddTransient<IEvaluationEngine, PlaceholderEvaluationEngine>();
        services.AddTransient<IPluginManager, PlaceholderPluginManager>();
        services.AddTransient<IWorkflowEngine, ConvoLabWorkflowEngine>();
        return services;
    }
}
