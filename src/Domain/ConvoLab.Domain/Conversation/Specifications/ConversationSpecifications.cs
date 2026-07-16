using ConvoLab.Domain.Conversation.Aggregates;
using ConvoLab.Domain.Conversation.Enums;

namespace ConvoLab.Domain.Conversation.Specifications;

public static class ConversationSpecifications
{
    public static bool CanReceiveMessages(Aggregates.Conversation aggregate)
    {
        return aggregate.Status != ConversationStatus.Archived && 
               aggregate.Status != ConversationStatus.Completed && 
               aggregate.Status != ConversationStatus.SoftDeleted;
    }

    public static bool CanBeArchived(Aggregates.Conversation aggregate)
    {
        return aggregate.Status != ConversationStatus.Active && 
               aggregate.Status != ConversationStatus.SoftDeleted;
    }

    public static bool CanResume(Aggregates.Conversation aggregate)
    {
        return aggregate.Status != ConversationStatus.Completed && 
               aggregate.Status != ConversationStatus.SoftDeleted;
    }

    public static bool HasActiveSession(Aggregates.Conversation aggregate)
    {
        return aggregate.Sessions.Any(s => s.Status == SessionStatus.Active);
    }

    public static bool HasParticipants(Aggregates.Conversation aggregate)
    {
        return aggregate.Participants.Any();
    }

    public static bool HasWorkflow(Aggregates.Conversation aggregate)
    {
        return aggregate.WorkflowExecutionIds.Any();
    }
}
