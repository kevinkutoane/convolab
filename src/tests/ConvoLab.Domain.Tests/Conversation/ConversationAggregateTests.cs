using ConvoLab.Domain.Conversation.Aggregates;
using ConvoLab.Domain.Conversation.Entities;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;
using ConvoLab.Domain.Users.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Conversation;

public class ConversationAggregateTests
{
    private readonly UserId _creatorId = UserId.CreateUnique();
    private readonly string _title = "Test Conversation";
    private readonly ConversationMetadata _metadata = ConversationMetadata.Create(new Dictionary<string, string>());
    private readonly ConversationWindow _window = ConversationWindow.Create(DateTime.UtcNow);
    private readonly ConversationContext _context = ConversationContext.Create();

    [Fact]
    public void Create_Should_Initialize_With_Correct_State()
    {
        // Act
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);

        // Assert
        Assert.Equal(ConversationStatus.Created, conversation.Status);
        Assert.Equal(_title, conversation.Title);
        Assert.Equal(_creatorId, conversation.CreatorId);
        Assert.Single(conversation.Timeline.Entries);
    }

    [Fact]
    public void Start_Should_Transition_Status_And_Add_Timeline_Entry()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);

        // Act
        conversation.Start();

        // Assert
        Assert.Equal(ConversationStatus.Started, conversation.Status);
        Assert.Contains(conversation.Timeline.Entries, e => e.EventName == "Conversation Started");
    }

    [Fact]
    public void Invalid_Status_Transition_Should_Throw_Exception()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => conversation.Complete());
    }

    [Fact]
    public void AddParticipant_Should_Add_New_Participant()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        var userId = UserId.CreateUnique();

        // Act
        conversation.AddParticipant(userId, ParticipantRole.Customer);

        // Assert
        Assert.Contains(conversation.Participants, p => p.UserId == userId);
        Assert.Contains(conversation.Timeline.Entries, e => e.EventName == "Participant Joined");
    }

    [Fact]
    public void AddMessage_Should_Add_Message_And_Link_To_Active_Session()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        conversation.Start();
        conversation.Activate();
        
        var participantId = ParticipantId.CreateUnique();
        conversation.StartSession(new[] { participantId });
        
        var message = ConversationMessage.Create(ParticipantRole.Customer, "Hello", participantId);

        // Act
        conversation.AddMessage(message);

        // Assert
        Assert.Contains(conversation.Messages, m => m.Id == message.Id);
        Assert.Contains(conversation.Sessions.First().MessageIds, id => id == message.Id);
    }

    [Fact]
    public void UpdateMemory_Should_Replace_Memory_Of_Same_Type()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        var strategy = MemoryStrategy.Create("Test");
        var window = MemoryWindow.Create(10, "Messages");
        
        var memory1 = ConversationMemory.Create(strategy, window, "Memory 1", MemoryType.ShortTerm);
        var memory2 = ConversationMemory.Create(strategy, window, "Memory 2", MemoryType.ShortTerm);

        // Act
        conversation.UpdateMemory(memory1);
        conversation.UpdateMemory(memory2);

        // Assert
        Assert.Single(conversation.Memories);
        Assert.Equal("Memory 2", conversation.Memories.First().Content);
    }
}
