using System.Reflection;
using ConvoLab.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.Services;
using ConvoLab.Application.Simulation;
using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Application.PromptStudio;
using ConvoLab.Application.WorkflowStudio;
using ConvoLab.Application.IntelligenceStudio;
using ConvoLab.Application.EvaluationStudio;
using ConvoLab.Application.TraceStudio;
using ConvoLab.Application.ReplayStudio;
using ConvoLab.Application.PolicyStudio;
using ConvoLab.Application.PluginStudio;
namespace ConvoLab.Application;
public static class DependencyInjection {
    public static IServiceCollection AddApplication(this IServiceCollection services) {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });
        // Capability 5 — Knowledge Engine: repositories, retriever, governance policy, and the engine.
        // Capability 6 — Intelligence Engine: catalogue, executions, budgets, planner, executor port, and the engine.
        services.AddSingleton<ConvoLab.Domain.Intelligence.Interfaces.IIntelligenceProviderRepository, RuntimeIntelligenceProviderRepository>();
        services.AddSingleton<ConvoLab.Domain.Intelligence.Interfaces.IExecutionRequestRepository, RuntimeExecutionRequestRepository>();
        services.AddSingleton<ConvoLab.Domain.Intelligence.Interfaces.IExecutionBudgetRepository, RuntimeExecutionBudgetRepository>();
        services.AddSingleton<ConvoLab.Domain.Intelligence.Services.ExecutionPlanner>();
        services.AddSingleton<IIntelligenceEngine, IntelligenceEngine>();
        services.AddSingleton<IIntelligenceCatalogueBootstrapper, IntelligenceCatalogueBootstrapper>();
        services.AddSingleton<IEvaluationEngine, EvaluationEngine>();
        services.AddScoped<PluginStudioService>();
        services.AddScoped<IPluginStudioService>(provider => provider.GetRequiredService<PluginStudioService>());
        services.AddScoped<IPluginManager>(provider => provider.GetRequiredService<PluginStudioService>());
        services.AddScoped<IKnowledgeStudioService, KnowledgeStudioService>();
        services.AddScoped<IPromptStudioService, PromptStudioService>();
        services.AddScoped<IWorkflowStudioService, WorkflowStudioService>();
        services.AddScoped<IConversationSimulationService, ConversationSimulationService>();
        services.AddScoped<IIntelligenceStudioService, IntelligenceStudioService>();
        services.AddScoped<IEvaluationStudioService, EvaluationStudioService>();
        services.AddScoped<ILegacyEvaluationStudioService, LegacyEvaluationStudioService>();
        services.AddScoped<ITraceStudioService, TraceStudioService>();
        services.AddScoped<IReplayStudioService, ReplayStudioService>();
        services.AddScoped<PolicyStudioService>();
        services.AddScoped<IPolicyStudioService>(provider => provider.GetRequiredService<PolicyStudioService>());
        services.AddScoped<IPolicyDecisionService>(provider => provider.GetRequiredService<PolicyStudioService>());
        return services;
    }
}
