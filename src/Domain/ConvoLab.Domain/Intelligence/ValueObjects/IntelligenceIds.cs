using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Intelligence.ValueObjects;

/// <summary>Strongly-typed identifier for an IntelligenceProvider aggregate.</summary>
public class IntelligenceProviderId : ValueObject
{
    public Guid Value { get; private set; }
    private IntelligenceProviderId(Guid value) => Value = value;
    private IntelligenceProviderId() { } // For EF Core
    public static IntelligenceProviderId CreateUnique() => new(Guid.NewGuid());
    public static IntelligenceProviderId FromGuid(Guid value) => new(value);
    public static implicit operator Guid(IntelligenceProviderId id) => id.Value;
    public override string ToString() => Value.ToString();
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}

/// <summary>Strongly-typed identifier for an IntelligenceModel entity.</summary>
public class IntelligenceModelId : ValueObject
{
    public Guid Value { get; private set; }
    private IntelligenceModelId(Guid value) => Value = value;
    private IntelligenceModelId() { } // For EF Core
    public static IntelligenceModelId CreateUnique() => new(Guid.NewGuid());
    public static IntelligenceModelId FromGuid(Guid value) => new(value);
    public static implicit operator Guid(IntelligenceModelId id) => id.Value;
    public override string ToString() => Value.ToString();
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}

/// <summary>Strongly-typed identifier for an ExecutionRequest aggregate.</summary>
public class ExecutionRequestId : ValueObject
{
    public Guid Value { get; private set; }
    private ExecutionRequestId(Guid value) => Value = value;
    private ExecutionRequestId() { } // For EF Core
    public static ExecutionRequestId CreateUnique() => new(Guid.NewGuid());
    public static ExecutionRequestId FromGuid(Guid value) => new(value);
    public static implicit operator Guid(ExecutionRequestId id) => id.Value;
    public override string ToString() => Value.ToString();
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}

/// <summary>Strongly-typed identifier for an ExecutionPlan.</summary>
public class ExecutionPlanId : ValueObject
{
    public Guid Value { get; private set; }
    private ExecutionPlanId(Guid value) => Value = value;
    private ExecutionPlanId() { } // For EF Core
    public static ExecutionPlanId CreateUnique() => new(Guid.NewGuid());
    public static ExecutionPlanId FromGuid(Guid value) => new(value);
    public static implicit operator Guid(ExecutionPlanId id) => id.Value;
    public override string ToString() => Value.ToString();
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}

/// <summary>Strongly-typed identifier for a StreamingSession entity.</summary>
public class StreamingSessionId : ValueObject
{
    public Guid Value { get; private set; }
    private StreamingSessionId(Guid value) => Value = value;
    private StreamingSessionId() { } // For EF Core
    public static StreamingSessionId CreateUnique() => new(Guid.NewGuid());
    public static StreamingSessionId FromGuid(Guid value) => new(value);
    public static implicit operator Guid(StreamingSessionId id) => id.Value;
    public override string ToString() => Value.ToString();
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}

/// <summary>Strongly-typed identifier for a ToolInvocation entity.</summary>
public class ToolInvocationId : ValueObject
{
    public Guid Value { get; private set; }
    private ToolInvocationId(Guid value) => Value = value;
    private ToolInvocationId() { } // For EF Core
    public static ToolInvocationId CreateUnique() => new(Guid.NewGuid());
    public static ToolInvocationId FromGuid(Guid value) => new(value);
    public static implicit operator Guid(ToolInvocationId id) => id.Value;
    public override string ToString() => Value.ToString();
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}

/// <summary>Strongly-typed identifier for an ExecutionBudget aggregate.</summary>
public class ExecutionBudgetId : ValueObject
{
    public Guid Value { get; private set; }
    private ExecutionBudgetId(Guid value) => Value = value;
    private ExecutionBudgetId() { } // For EF Core
    public static ExecutionBudgetId CreateUnique() => new(Guid.NewGuid());
    public static ExecutionBudgetId FromGuid(Guid value) => new(value);
    public static implicit operator Guid(ExecutionBudgetId id) => id.Value;
    public override string ToString() => Value.ToString();
    protected override IEnumerable<object> GetEqualityComponents() { yield return Value; }
}
