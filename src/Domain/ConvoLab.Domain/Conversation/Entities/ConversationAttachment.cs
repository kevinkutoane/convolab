using ConvoLab.Domain.Common;
using ConvoLab.Domain.Conversation.Enums;
using ConvoLab.Domain.Conversation.ValueObjects;

namespace ConvoLab.Domain.Conversation.Entities;

public class ConversationAttachment : BaseEntity<AttachmentId>
{
    public string FileName { get; private set; }
    public string FilePath { get; private set; }
    public AttachmentType Type { get; private set; }
    public DateTime UploadedAt { get; private set; }

    private ConversationAttachment(AttachmentId id, string fileName, string filePath, AttachmentType type, DateTime uploadedAt)
        : base(id)
    {
        FileName = fileName;
        FilePath = filePath;
        Type = type;
        UploadedAt = uploadedAt;
    }

    public static ConversationAttachment Create(string fileName, string filePath, AttachmentType type)
    {
        return new(AttachmentId.CreateUnique(), fileName, filePath, type, DateTime.UtcNow);
    }

    private ConversationAttachment() { FileName = null!; FilePath = null!; }
}

