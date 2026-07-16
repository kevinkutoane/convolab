namespace ConvoLab.Domain.Prompt.Enums;

/// <summary>
/// Represents the lifecycle state of a Prompt aggregate.
/// </summary>
public enum PromptStatus
{
    /// <summary>The prompt is being authored and has not been submitted for review.</summary>
    Draft = 0,

    /// <summary>The prompt has been submitted and is awaiting approval.</summary>
    InReview = 1,

    /// <summary>The prompt has been approved and is ready for production use.</summary>
    Approved = 2,

    /// <summary>The prompt is actively being used in production.</summary>
    Active = 3,

    /// <summary>The prompt has been superseded by a newer version but remains usable.</summary>
    Deprecated = 4,

    /// <summary>The prompt has been archived and is no longer usable.</summary>
    Archived = 5
}
