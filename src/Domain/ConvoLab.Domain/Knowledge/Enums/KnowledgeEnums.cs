namespace ConvoLab.Domain.Knowledge.Enums;

/// <summary>
/// The type of enterprise system a knowledge source represents.
/// These are abstractions only — no integration is implemented at the domain level.
/// </summary>
public enum KnowledgeSourceType
{
    SharePoint,
    Confluence,
    MicrosoftTeams,
    Dynamics365,
    Salesforce,
    SqlDatabase,
    PostgreSql,
    AzureSql,
    Oracle,
    PdfDocument,
    WordDocument,
    Markdown,
    Website,
    RestApi,
    GraphQlApi,
    InternalMicroservice,
    KnowledgeGraph,
    VectorDatabase,
    FileSystem,
    Custom
}

/// <summary>Lifecycle status of a knowledge asset (source, document, or version).</summary>
public enum KnowledgeLifecycleStatus
{
    Draft,
    PendingApproval,
    Approved,
    Published,
    Deprecated,
    Archived
}

/// <summary>Enterprise classification level applied to knowledge for governance.</summary>
public enum KnowledgeClassification
{
    Public,
    Internal,
    Confidential,
    Restricted,
    HighlySensitive
}

/// <summary>Sensitivity labels that policies can attach to knowledge assets.</summary>
public enum SensitivityLabel
{
    None,
    PersonalData,
    FinancialData,
    HealthData,
    LegalPrivileged,
    TradeSecret
}

/// <summary>
/// The retrieval strategy requested by a consumer (Conversation/Workflow) when querying knowledge.
/// The strategy is a domain concept; the actual search implementation lives in infrastructure.
/// </summary>
public enum RetrievalStrategyType
{
    Keyword,
    Semantic,
    Hybrid,
    Filtered,
    Metadata,
    PolicyBased,
    Recency,
    ConfidenceBased,
    ConversationAware,
    WorkflowAware
}

/// <summary>The structural type of a knowledge chunk produced by the chunking model.</summary>
public enum ChunkType
{
    Paragraph,
    Section,
    Semantic,
    Table,
    Document,
    ImageReference
}

/// <summary>Operational status of a knowledge connector.</summary>
public enum ConnectorStatus
{
    Registered,
    Validating,
    Active,
    Syncing,
    Degraded,
    Failed,
    Disabled
}

/// <summary>Health rating reported for a source or connector.</summary>
public enum HealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>How a connector synchronizes content from its source system.</summary>
public enum SynchronizationMode
{
    Manual,
    Scheduled,
    EventDriven,
    Continuous
}
