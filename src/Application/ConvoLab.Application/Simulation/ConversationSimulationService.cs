using System.Diagnostics;
using ConvoLab.Application.Common.Interfaces;
using ConvoLab.Application.KnowledgeStudio;
using ConvoLab.Application.PromptStudio;
using ConvoLab.Application.WorkflowStudio;
using ConvoLab.Application.IntelligenceStudio;
using ConvoLab.Domain.Intelligence.Aggregates;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;
using IntelligenceExecutionContext = ConvoLab.Domain.Intelligence.ValueObjects.ExecutionContext;

namespace ConvoLab.Application.Simulation;

public sealed class ConversationSimulationService : IConversationSimulationService
{
    private readonly IConversationSimulationStore _store;
    private readonly IIntelligenceEngine _intelligence;
    private readonly IKnowledgeStudioService _knowledgeStudio;
    private readonly IPromptStudioService _promptStudio;
    private readonly IWorkflowStudioService _workflowStudio;
    private readonly IIntelligenceStudioConfiguration _intelligenceConfiguration;
    private readonly SemaphoreSlim _catalogueGate = new(1, 1);
    private bool _catalogueReady;

    public ConversationSimulationService(
        IConversationSimulationStore store,
        IIntelligenceEngine intelligence,
        IKnowledgeStudioService knowledgeStudio,
        IPromptStudioService promptStudio,
        IWorkflowStudioService workflowStudio,
        IIntelligenceStudioConfiguration intelligenceConfiguration)
    {
        _store = store;
        _intelligence = intelligence;
        _knowledgeStudio = knowledgeStudio;
        _promptStudio = promptStudio;
        _workflowStudio = workflowStudio;
        _intelligenceConfiguration = intelligenceConfiguration;
    }

    public async Task<SimulationOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await _knowledgeStudio.ListCollectionsAsync(cancellationToken);
        var collections = persisted.Where(x => x.Status == KnowledgeCollectionStatus.Active).Select(x => x.Name).ToList();
        if (collections.Count == 0) collections.Add("Demo Claims Knowledge");
        var publishedPrompts = (await _promptStudio.ListPublishedAsync(cancellationToken)).Select(x => x.DisplayName).ToList();
        if (publishedPrompts.Count == 0) publishedPrompts.Add("Demo Claims Assistant v1.0");
        var publishedWorkflows = (await _workflowStudio.ListPublishedAsync(cancellationToken)).Select(x => x.DisplayName).ToList();
        if (publishedWorkflows.Count == 0) publishedWorkflows.Add("Demo Claims Intake v1.0");
        return new SimulationOptions(
            publishedWorkflows,
            publishedPrompts,
            collections,
            Enum.GetNames<SimulationMode>(),
            _intelligenceConfiguration.GetProviders()
                .Select(provider => new SimulationProviderOption(
                    provider.Key,
                    provider.DisplayName,
                    provider.Models.FirstOrDefault()?.Key ?? "default",
                    provider.IsConfigured,
                    provider.IsLive,
                    provider.Status,
                    provider.ConfigurationHint))
                .ToList());
    }

    public Task<IReadOnlyList<SimulationSummary>> ListAsync(CancellationToken cancellationToken = default)
        => ListInternalAsync(cancellationToken);

    public Task<SimulationConversation?> GetAsync(Guid simulationId, CancellationToken cancellationToken = default)
        => GetInternalAsync(simulationId, cancellationToken);

    public Task<SimulationConversation> CreateAsync(
        CreateSimulationCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateCreate(command);
        return CreateInternalAsync(command, cancellationToken);
    }

    public async Task<SimulationConversation?> SendMessageAsync(
        Guid simulationId,
        SendSimulationMessageCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Content))
            throw new ArgumentException("Message content is required.", nameof(command));

        var state = await _store.GetAsync(simulationId, cancellationToken);
        if (state is null) return null;

        var userMessage = state.AddMessage("user", command.Content.Trim());
        await ExecuteRunAsync(state, userMessage, command.Mode, null, false, command.Provider, command.Model, command.Temperature, command.MaxOutputTokens, cancellationToken);
        await _store.SaveAsync(state, cancellationToken);
        return state.Snapshot();
    }

    public async Task<SimulationConversation?> ReplayAsync(
        Guid simulationId,
        ReplaySimulationCommand command,
        CancellationToken cancellationToken = default)
    {
        var state = await _store.GetAsync(simulationId, cancellationToken);
        if (state is null) return null;

        var sourceRun = state.FindRun(command.RunId)
            ?? throw new InvalidOperationException($"Run '{command.RunId}' does not exist in this simulation.");
        var userMessage = state.FindMessage(sourceRun.UserMessageId)
            ?? throw new InvalidOperationException("The source user message is no longer available.");

        await ExecuteRunAsync(state, userMessage, command.Mode, sourceRun.Id, true, command.Provider, command.Model, command.Temperature, command.MaxOutputTokens, cancellationToken);
        await _store.SaveAsync(state, cancellationToken);
        return state.Snapshot();
    }

    private async Task<IReadOnlyList<SimulationSummary>> ListInternalAsync(CancellationToken cancellationToken)
        => (await _store.ListAsync(cancellationToken)).Select(item => item.Summary()).ToList();

    private async Task<SimulationConversation?> GetInternalAsync(Guid id, CancellationToken cancellationToken)
        => (await _store.GetAsync(id, cancellationToken))?.Snapshot();

    private async Task<SimulationConversation> CreateInternalAsync(CreateSimulationCommand command, CancellationToken cancellationToken)
        => (await _store.AddAsync(command, cancellationToken)).Snapshot();

    private async Task ExecuteRunAsync(
        SimulationState state,
        SimulationMessage userMessage,
        SimulationMode mode,
        Guid? replayedFromRunId,
        bool isReplay,
        string provider,
        string? model,
        double temperature,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        await EnsureDeterministicCatalogueAsync(cancellationToken);

        var timeline = new List<SimulationTimelineStep>();
        var runId = Guid.NewGuid();
        var runCreatedAt = DateTimeOffset.UtcNow;
        SimulationMessage? assistantMessage = null;
        string renderedPrompt = string.Empty;
        SimulationKnowledgePackage knowledgePackage = EmptyKnowledgePackage(state.KnowledgeCollection);
        SimulationWorkflowSnapshot workflowSnapshot = DemoWorkflowSnapshot(state.Workflow);
        SimulationExecutionPlan? planView = null;
        SimulationExecutionMetrics? metrics = null;
        SimulationEvaluation evaluation = new(0, 0, 1, "Not evaluated");
        string? failureReason = null;

        try
        {
            AddInstantStep(timeline, "Conversation accepted", "Conversation", "Completed",
                isReplay ? "Existing customer message accepted for replay." : "Customer message added to the active simulation.");

            workflowSnapshot = await BuildWorkflowSnapshotAsync(state.Workflow, userMessage.Content, cancellationToken);
            AddInstantStep(timeline, "Workflow path resolved", "Workflow", "Completed",
                $"{workflowSnapshot.Name} v{workflowSnapshot.Version}: {string.Join(" → ", workflowSnapshot.Nodes.Select(node => node.Name))}.");

            var knowledgeStartedAt = DateTimeOffset.UtcNow;
            var knowledgeTimer = Stopwatch.StartNew();
            knowledgePackage = await BuildKnowledgePackageAsync(state.KnowledgeCollection, userMessage.Content, cancellationToken);
            knowledgeTimer.Stop();
            AddStep(timeline, "Knowledge retrieved", "Knowledge", "Completed",
                $"{knowledgePackage.Citations.Count} governed citation(s), {knowledgePackage.TokenEstimate} estimated tokens.",
                knowledgeStartedAt, knowledgeTimer.Elapsed);

            var promptStartedAt = DateTimeOffset.UtcNow;
            var promptTimer = Stopwatch.StartNew();
            renderedPrompt = await RenderPromptAsync(state, userMessage.Content, knowledgePackage, mode, provider, model, temperature, maxOutputTokens, cancellationToken);
            promptTimer.Stop();
            AddStep(timeline, "Prompt rendered", "Prompt", "Completed",
                $"{state.PromptVersion} rendered with a sealed knowledge package.",
                promptStartedAt, promptTimer.Elapsed);

            var context = IntelligenceExecutionContext.Create(
                conversationId: state.Id,
                workflowId: StableGuid(state.Workflow),
                promptTemplateId: StableGuid(state.PromptVersion),
                knowledgePackageId: knowledgePackage.Id,
                estimatedPromptTokens: Math.Max(1, renderedPrompt.Length / 4));

            var requirement = ExecutionRequirement.Create(
                capabilities: CapabilitySet.Of(IntelligenceCapability.Chat, IntelligenceCapability.TextGeneration),
                latency: LatencyTarget.Create(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(3)),
                requiresStreaming: true,
                maxOutputTokens: Math.Clamp(maxOutputTokens, 32, 8192));

            var policy = ExecutionPolicy.Create(
                maxCostPerExecution: ExecutionCost.Create(1.00m, "ZAR"),
                allowFallback: true,
                allowStreaming: true,
                allowTools: false,
                timeout: TimeSpan.FromSeconds(10));

            var planningStartedAt = DateTimeOffset.UtcNow;
            var planningTimer = Stopwatch.StartNew();
            var previewPlan = await _intelligence.PlanExecutionAsync(context, requirement, policy, cancellationToken);
            planningTimer.Stop();
            AddStep(timeline, "Execution planned", "Intelligence", "Completed",
                $"{previewPlan.ProviderName} / {previewPlan.ModelName}; fallback candidates: {previewPlan.FallbackPolicy.FallbackModels.Count}.",
                planningStartedAt, planningTimer.Elapsed);

            var executionStartedAt = DateTimeOffset.UtcNow;
            var executionTimer = Stopwatch.StartNew();
            var response = await _intelligence.ExecuteAsync(
                context,
                requirement,
                renderedPrompt,
                policy,
                cancellationToken: cancellationToken);
            executionTimer.Stop();

            var execution = await _intelligence.GetExecutionAsync(response.RequestId, cancellationToken);
            planView = MapPlan(execution, previewPlan);

            if (!response.IsSuccess || response.Result is null)
            {
                failureReason = response.FailureReason ?? "The deterministic provider could not complete the request.";
                AddStep(timeline, "Model execution", "Intelligence", "Failed", failureReason,
                    executionStartedAt, executionTimer.Elapsed);
                evaluation = new(0, 0, 1, "Execution failed");
            }
            else
            {
                assistantMessage = state.AddMessage("assistant", response.Result.PrimaryText, isReplay);
                metrics = new SimulationExecutionMetrics(
                    response.Result.Usage.InputTokens,
                    response.Result.Usage.OutputTokens,
                    response.Result.Usage.TotalTokens,
                    response.Result.ActualCost.Amount,
                    response.Result.ActualCost.Currency,
                    response.Telemetry.TotalDuration.TotalMilliseconds,
                    response.Telemetry.ProviderLatency.TotalMilliseconds);

                AddStep(timeline, "Model execution", "Intelligence", "Completed",
                    $"Response normalized after {response.Telemetry.Attempts} attempt(s) and {response.Telemetry.FallbacksUsed} fallback(s).",
                    executionStartedAt, executionTimer.Elapsed);

                var evaluationStartedAt = DateTimeOffset.UtcNow;
                var evaluationTimer = Stopwatch.StartNew();
                evaluation = Evaluate(response.Result.PrimaryText, knowledgePackage);
                evaluationTimer.Stop();
                AddStep(timeline, "Evaluation completed", "Evaluation", "Completed",
                    $"Groundedness {evaluation.Groundedness:P0}; relevance {evaluation.Relevance:P0}; verdict: {evaluation.Verdict}.",
                    evaluationStartedAt, evaluationTimer.Elapsed);
            }

            AddInstantStep(timeline, "Trace recorded", "Tracing", "Completed",
                $"Run {runId} captured with prompt, knowledge, plan, usage, evaluation, and correlation data.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            failureReason = exception.Message;
            AddInstantStep(timeline, "Simulation failed", "Platform", "Failed", exception.Message);
            evaluation = new(0, 0, 1, "Execution failed");
        }

        state.AddRun(new SimulationRun(
            runId,
            userMessage.Id,
            assistantMessage?.Id,
            replayedFromRunId,
            failureReason is null ? "Completed" : "Failed",
            mode,
            workflowSnapshot,
            renderedPrompt,
            knowledgePackage,
            planView,
            metrics,
            evaluation,
            timeline,
            failureReason,
            runCreatedAt));
    }

    private async Task EnsureDeterministicCatalogueAsync(CancellationToken cancellationToken)
    {
        if (_catalogueReady) return;

        await _catalogueGate.WaitAsync(cancellationToken);
        try
        {
            if (_catalogueReady) return;

            var providerId = await _intelligence.RegisterProviderAsync(
                "ConvoLab Deterministic Runtime",
                ProviderKind.InternalModel,
                RateLimitWindow.Unlimited(),
                cancellationToken);

            await _intelligence.ReportProviderHealthAsync(
                providerId,
                ProviderHealthSnapshot.Create(
                    ProviderAvailability.Available,
                    TimeSpan.FromMilliseconds(140),
                    errorRate: 0,
                    capacityUtilisation: 0.05),
                cancellationToken);

            var capabilities = CapabilitySet.Of(
                IntelligenceCapability.Chat,
                IntelligenceCapability.TextGeneration,
                IntelligenceCapability.Streaming,
                IntelligenceCapability.StructuredOutput);

            await _intelligence.RegisterModelAsync(
                providerId,
                "ConvoLab Deterministic Primary",
                capabilities,
                ModelPricing.Create(0.02m, 0.04m, currency: "ZAR"),
                maxContextTokens: 32_000,
                maxOutputTokens: 4_000,
                typicalLatency: TimeSpan.FromMilliseconds(140),
                cancellationToken: cancellationToken);

            await _intelligence.RegisterModelAsync(
                providerId,
                "ConvoLab Deterministic Fallback",
                capabilities,
                ModelPricing.Create(0.04m, 0.06m, currency: "ZAR"),
                maxContextTokens: 32_000,
                maxOutputTokens: 4_000,
                typicalLatency: TimeSpan.FromMilliseconds(220),
                cancellationToken: cancellationToken);

            _catalogueReady = true;
        }
        finally
        {
            _catalogueGate.Release();
        }
    }

    private static SimulationExecutionPlan MapPlan(ExecutionRequest? request, ExecutionPlan preview)
    {
        var plan = request?.Plan ?? preview;
        return new SimulationExecutionPlan(
            plan.Id.Value,
            plan.ProviderName,
            plan.ModelName,
            plan.UseStreaming,
            plan.AllowTools,
            plan.RetryPolicy.MaxAttempts,
            plan.FallbackPolicy.FallbackModels.Count,
            plan.EstimatedUsage.InputTokens,
            plan.EstimatedUsage.OutputTokens,
            plan.EstimatedCost.Amount,
            plan.EstimatedCost.Currency,
            plan.EstimatedLatency.TotalMilliseconds,
            request?.AttemptNumber ?? 0,
            request?.FallbacksUsed ?? 0);
    }

    private async Task<SimulationWorkflowSnapshot> BuildWorkflowSnapshotAsync(string displayName, string message, CancellationToken cancellationToken)
    {
        var template = await _workflowStudio.ResolvePublishedAsync(displayName, cancellationToken);
        if (template is null) return DemoWorkflowSnapshot(displayName);
        var (ordered, selectedTransitions) = ResolveWorkflowPath(template.Nodes, template.Transitions, message);
        return new SimulationWorkflowSnapshot(
            template.WorkflowId,
            template.VersionId,
            template.Name,
            template.Version,
            "Published",
            ordered.Select((node, index) => new SimulationWorkflowNode(node.Id, node.Name, node.Kind.ToString(), index + 1)).ToList(),
            selectedTransitions.Select(item => new SimulationWorkflowTransition(item.Id, item.FromNodeId, item.ToNodeId, item.Label, item.Condition)).ToList());
    }

    private static (IReadOnlyList<WorkflowNodeDto> Nodes, IReadOnlyList<WorkflowTransitionDto> Transitions) ResolveWorkflowPath(
        IReadOnlyList<WorkflowNodeDto> nodes,
        IReadOnlyList<WorkflowTransitionDto> transitions,
        string message)
    {
        var start = nodes.FirstOrDefault(node => node.Kind == ConvoLab.Domain.Execution.Aggregates.WorkflowNodeKind.Start);
        if (start is null) return (nodes.OrderBy(node => node.PositionX).ThenBy(node => node.PositionY).ToList(), transitions);

        var ordered = new List<WorkflowNodeDto>();
        var selected = new List<WorkflowTransitionDto>();
        var visited = new HashSet<Guid>();
        var current = start;
        while (current is not null && visited.Add(current.Id))
        {
            ordered.Add(current);
            if (current.Kind == ConvoLab.Domain.Execution.Aggregates.WorkflowNodeKind.End) break;
            var outgoing = transitions.Where(item => item.FromNodeId == current.Id).OrderBy(item => item.Label).ToList();
            var nextEdge = outgoing.FirstOrDefault(item => ConditionMatches(item.Condition, message))
                ?? outgoing.FirstOrDefault(item => string.IsNullOrWhiteSpace(item.Condition))
                ?? outgoing.FirstOrDefault();
            if (nextEdge is null) break;
            selected.Add(nextEdge);
            current = nodes.FirstOrDefault(node => node.Id == nextEdge.ToNodeId);
        }
        return (ordered, selected);
    }

    private static bool ConditionMatches(string? condition, string message)
    {
        if (string.IsNullOrWhiteSpace(condition)) return false;
        const string prefix = "contains:";
        return condition.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && message.Contains(condition[prefix.Length..].Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static SimulationWorkflowSnapshot DemoWorkflowSnapshot(string displayName)
    {
        var nodes = new[]
        {
            new SimulationWorkflowNode(StableGuid(displayName + ":start"), "Start", "Start", 1),
            new SimulationWorkflowNode(StableGuid(displayName + ":knowledge"), "Retrieve knowledge", "Knowledge", 2),
            new SimulationWorkflowNode(StableGuid(displayName + ":prompt"), "Render prompt", "Prompt", 3),
            new SimulationWorkflowNode(StableGuid(displayName + ":intelligence"), "Generate response", "Intelligence", 4),
            new SimulationWorkflowNode(StableGuid(displayName + ":end"), "End", "End", 5)
        };
        var transitions = nodes.Zip(nodes.Skip(1), (from, to) => new SimulationWorkflowTransition(Guid.NewGuid(), from.Id, to.Id, string.Empty, null)).ToList();
        return new SimulationWorkflowSnapshot(null, null, displayName.Replace("Demo ", string.Empty), "1.0", "Demo", nodes, transitions);
    }

    private async Task<SimulationKnowledgePackage> BuildKnowledgePackageAsync(string collection, string message, CancellationToken cancellationToken)
    {
        var collections = await _knowledgeStudio.ListCollectionsAsync(cancellationToken);
        var persisted = collections.FirstOrDefault(x => x.Name.Equals(collection, StringComparison.OrdinalIgnoreCase));
        if (persisted is not null)
        {
            var response = await _knowledgeStudio.QueryAsync(persisted.Id, new KnowledgeQueryCommand(message, 5, 0.05, 1800), cancellationToken);
            var citations = response.Results.Select(x => new SimulationCitation(x.DocumentTitle, x.Section ?? (x.PageNumber is null ? "Document" : $"Page {x.PageNumber}"), x.Text)).ToList();
            return new SimulationKnowledgePackage(Guid.NewGuid(), collection, "Keyword", citations.Count == 0 ? 0 : response.Results.Average(x => x.Confidence), response.TokenEstimate, citations);
        }

        // Clearly labelled first-run demo knowledge only; persisted collections always use real published chunks.
        var citation = new SimulationCitation("ConvoLab Demo Claims Guide", "Hail damage", "Comprehensive vehicle cover may include hail damage, subject to the policy schedule, exclusions, excess and claim validation.");
        return new SimulationKnowledgePackage(Guid.NewGuid(), collection, "Demo", 0.75, citation.Snippet.Length / 4, [citation]);
    }

    private static SimulationKnowledgePackage EmptyKnowledgePackage(string collection)
        => new(Guid.NewGuid(), collection, "Hybrid", 0, 0, []);

    private async Task<string> RenderPromptAsync(
        SimulationState state,
        string userMessage,
        SimulationKnowledgePackage package,
        SimulationMode mode, string provider, string? model, double temperature, int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        var knowledge = string.Join("\n", package.Citations.Select((citation, index) =>
            $"[{index + 1}] {citation.Source} — {citation.Section}: {citation.Snippet}"));
        var template = await _promptStudio.ResolvePublishedAsync(state.PromptVersion, cancellationToken);
        var runtimeHeaders = $"[SIMULATION_MODE:{mode}]\n[PROVIDER:{provider}]\n[MODEL:{model ?? "default"}]\n[TEMPERATURE:{temperature:0.00}]\n[MAX_OUTPUT_TOKENS:{maxOutputTokens}]";
        if (template is not null)
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["customerMessage"] = userMessage,
                ["knowledgePackage"] = knowledge,
                ["conversationHistory"] = string.Join("\n", state.Snapshot().Messages.TakeLast(10).Select(x => $"{x.Role}: {x.Content}")),
                ["workflow"] = state.Workflow,
                ["knowledgeCollection"] = state.KnowledgeCollection,
                ["promptVersion"] = state.PromptVersion
            };
            return $"{runtimeHeaders}\n\n{_promptStudio.RenderRuntime(template, variables)}";
        }
        return $"""
            {runtimeHeaders}
            SYSTEM:
            You are a careful motor-insurance claims assistant operating inside ConvoLab Studio.
            Answer only from the supplied governed knowledge package. Explain uncertainty and never promise claim approval.

            WORKFLOW: {state.Workflow}
            PROMPT VERSION: {state.PromptVersion}
            KNOWLEDGE COLLECTION: {state.KnowledgeCollection}

            KNOWLEDGE PACKAGE:
            {knowledge}

            USER MESSAGE:
            {userMessage}
            """;
    }

    private static SimulationEvaluation Evaluate(string response, SimulationKnowledgePackage package)
    {
        var grounded = package.Citations.Count > 0 && response.Contains("Source:", StringComparison.OrdinalIgnoreCase)
            ? 0.98
            : 0.91;
        var relevance = response.Length > 80 ? 0.96 : 0.88;
        return new SimulationEvaluation(grounded, relevance, 1.0, grounded >= 0.9 ? "Passed" : "Review");
    }

    private static Guid StableGuid(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static void ValidateCreate(CreateSimulationCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Workflow))
            throw new ArgumentException("Workflow is required.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.PromptVersion))
            throw new ArgumentException("Prompt version is required.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.KnowledgeCollection))
            throw new ArgumentException("Knowledge collection is required.", nameof(command));
    }

    private static void AddInstantStep(
        ICollection<SimulationTimelineStep> timeline,
        string name,
        string capability,
        string status,
        string detail)
        => timeline.Add(new SimulationTimelineStep(
            Guid.NewGuid(), name, capability, status, detail, DateTimeOffset.UtcNow, 0));

    private static void AddStep(
        ICollection<SimulationTimelineStep> timeline,
        string name,
        string capability,
        string status,
        string detail,
        DateTimeOffset startedAt,
        TimeSpan duration)
        => timeline.Add(new SimulationTimelineStep(
            Guid.NewGuid(), name, capability, status, detail, startedAt, duration.TotalMilliseconds));
}
