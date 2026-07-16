using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Entities;

/// <summary>
/// A single tool (or function) invocation requested by a model during an
/// execution. Provider-independent: the domain records what was asked, its
/// arguments as an opaque payload, and the outcome — never how the provider
/// wire format looked.
/// </summary>
public class ToolInvocation : BaseEntity<ToolInvocationId>
{
    public string ToolName { get; private set; }
    public ToolKind Kind { get; private set; }
    public string ArgumentsPayload { get; private set; }
    public ToolInvocationStatus Status { get; private set; }
    public string? ResultPayload { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>True for direct function-calling (a function tool), false for other tool kinds.</summary>
    public bool IsFunctionInvocation => Kind == ToolKind.Internal || Kind == ToolKind.Plugin;

    private ToolInvocation() : base()
    {
        ToolName = null!;
        ArgumentsPayload = null!;
    } // For EF Core

    internal ToolInvocation(ToolInvocationId id, string toolName, ToolKind kind, string argumentsPayload) : base(id)
    {
        if (string.IsNullOrWhiteSpace(toolName)) throw new ArgumentException("Tool name is required.");
        ToolName = toolName;
        Kind = kind;
        ArgumentsPayload = argumentsPayload ?? string.Empty;
        Status = ToolInvocationStatus.Requested;
        RequestedAt = DateTime.UtcNow;
    }

    public void Begin()
    {
        if (Status != ToolInvocationStatus.Requested)
            throw new InvalidOperationException($"Cannot begin a tool invocation in status '{Status}'.");
        Status = ToolInvocationStatus.Executing;
    }

    public void Complete(string resultPayload)
    {
        if (Status != ToolInvocationStatus.Executing)
            throw new InvalidOperationException($"Cannot complete a tool invocation in status '{Status}'.");
        Status = ToolInvocationStatus.Completed;
        ResultPayload = resultPayload ?? string.Empty;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        if (Status is ToolInvocationStatus.Completed or ToolInvocationStatus.Rejected)
            throw new InvalidOperationException($"Cannot fail a tool invocation in status '{Status}'.");
        Status = ToolInvocationStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>Rejected by policy before execution — tools are governed too.</summary>
    public void Reject(string reason)
    {
        if (Status != ToolInvocationStatus.Requested)
            throw new InvalidOperationException($"Cannot reject a tool invocation in status '{Status}'.");
        Status = ToolInvocationStatus.Rejected;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }
}
