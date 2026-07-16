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
    public void Resume_Should_Throw_If_Completed()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        conversation.Start();
        conversation.Resume(); // Active
        conversation.Complete();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => conversation.Resume());
    }

    [Fact]
    public void Archive_Should_Throw_If_Active()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        conversation.Start();
        conversation.Resume(); // Active

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => conversation.Archive());
    }

    [Fact]
    public void Restore_Should_Throw_If_SoftDeleted()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        conversation.ExpireConversation(); // SoftDeleted

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => conversation.Restore());
    }

    [Fact]
    public void StartSession_Should_Throw_If_Overlapping()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        var participantId = ParticipantId.CreateUnique();
        conversation.StartSession(new[] { participantId });

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => conversation.StartSession(new[] { participantId }));
    }

    [Fact]
    public void RemoveParticipant_Should_Throw_If_Final_Participant()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        var userId = UserId.CreateUnique();
        conversation.AddParticipant(userId, ParticipantRole.Customer);
        var participantId = conversation.Participants.First().Id;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => conversation.RemoveParticipant(participantId));
    }

    [Fact]
    public void AddMessage_Should_Throw_If_Archived()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        conversation.Start();
        conversation.Resume();
        conversation.Complete();
        conversation.Archive();

        var message = ConversationMessage.Create(ParticipantRole.Customer, MessageContent.FromString("Hello"), _creatorId, _metadata);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => conversation.AddMessage(message));
    }

    [Fact]
    public void UpdateMemory_Should_Support_Working_Memory()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        var strategy = MemoryStrategy.Create("Test");
        var window = MemoryWindow.Create(10, "Messages");
        var memory = ConversationMemory.Create(strategy, window, "Working data", MemoryType.Working);

        // Act
        conversation.UpdateMemory(memory);

        // Assert
        Assert.Contains(conversation.Memories, m => m.Type == MemoryType.Working);
    }

    [Fact]
    public void Statistics_Should_Reflect_Current_State()
    {
        // Arrange
        var conversation = ConvoLab.Domain.Conversation.Aggregates.Conversation.Create(_creatorId, _title, _metadata, _window, _context);
        conversation.Start();
        conversation.Resume();
        
        var participantId = ParticipantId.CreateUnique();
        conversation.StartSession(new[] { participantId });
        
        var message = ConversationMessage.Create(ParticipantRole.Customer, MessageContent.FromString("Hello"), _creatorId, _metadata);
        conversation.AddMessage(message);

        // Assert
        Assert.Equal(1, conversation.MessageCount);
        Assert.Equal(1, conversation.SessionCount);
        Assert.True(conversation.TimelineCount > 0);
    }
}
