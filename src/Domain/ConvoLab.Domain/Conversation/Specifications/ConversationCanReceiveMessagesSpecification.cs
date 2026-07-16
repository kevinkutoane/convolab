using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Aggregates;
using ConvoLab.Domain.Conversation.Enums;
using System.Linq.Expressions;

namespace ConvoLab.Domain.Conversation.Specifications;

public class ConversationCanReceiveMessagesSpecification : Specification<Aggregates.Conversation>
{
    public override Expression<Func<Aggregates.Conversation, bool>> ToExpression()
    {
        return conversation => conversation.Status == ConversationStatus.Active ||
                              conversation.Status == ConversationStatus.Started ||
                              conversation.Status == ConversationStatus.Paused;
    }
}
