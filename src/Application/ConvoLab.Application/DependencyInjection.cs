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
        services.AddTransient<IKnowledgeEngine, PlaceholderKnowledgeEngine>();
        services.AddTransient<IAIOrchestrator, PlaceholderAIOrchestrator>();
        services.AddTransient<ITraceEngine, PlaceholderTraceEngine>();
        services.AddTransient<IEvaluationEngine, PlaceholderEvaluationEngine>();
        services.AddTransient<IPluginManager, PlaceholderPluginManager>();
        services.AddTransient<IWorkflowEngine, ConvoLabWorkflowEngine>();
        return services;
    }
}
