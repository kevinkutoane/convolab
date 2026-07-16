namespace ConvoLab.Domain.Prompt.Enums;

/// <summary>
/// Defines the role of a section within a composed prompt.
/// </summary>
public enum PromptSectionType
{
    System = 0,
    Role = 1,
    Knowledge = 2,
    Safety = 3,
    ConversationMemory = 4,
    UserMessage = 5,
    Custom = 6
}
