using ConvoLab.Domain.Common;
using ConvoLab.Domain.Intelligence.Enums;
using ConvoLab.Domain.Intelligence.ValueObjects;

namespace ConvoLab.Domain.Intelligence.Entities;

/// <summary>
/// A first-class streaming session within an execution. Chunks arrive in
/// strict sequence; the session assembles content, tracks statistics
/// (including time-to-first-chunk), and produces a StreamingCompletion.
/// </summary>
public class StreamingSession : BaseEntity<StreamingSessionId>
{
    private readonly List<StreamingChunk> _chunks = new();

    public StreamingStatus Status { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? FirstChunkAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public StreamingCompletion? Completion { get; private set; }

    public IReadOnlyList<StreamingChunk> Chunks => _chunks.AsReadOnly();

    private StreamingSession() : base() { } // For EF Core

    internal StreamingSession(StreamingSessionId id) : base(id)
    {
        Status = StreamingStatus.Opened;
        OpenedAt = DateTime.UtcNow;
    }

    /// <summary>Appends the next chunk. Sequence must be strictly increasing from zero.</summary>
    public void AppendChunk(string content)
    {
        if (Status is StreamingStatus.Completed or StreamingStatus.Aborted)
            throw new InvalidOperationException("Cannot append chunks to a closed streaming session.");

        Status = StreamingStatus.Streaming;
        FirstChunkAt ??= DateTime.UtcNow;
        _chunks.Add(StreamingChunk.Create(_chunks.Count, content));
    }

    /// <summary>Closes the session and produces the completion with statistics.</summary>
    public StreamingCompletion Complete()
    {
        if (Status is StreamingStatus.Completed or StreamingStatus.Aborted)
            throw new InvalidOperationException("Streaming session is already closed.");

        ClosedAt = DateTime.UtcNow;
        Status = StreamingStatus.Completed;

        var content = string.Concat(_chunks.OrderBy(c => c.Sequence).Select(c => c.Content));
        var stats = StreamingStatistics.Create(
            _chunks.Count,
            (FirstChunkAt ?? ClosedAt.Value) - OpenedAt,
            ClosedAt.Value - OpenedAt,
            content.Length);

        Completion = StreamingCompletion.Create(content, stats);
        return Completion;
    }

    /// <summary>Aborts the session (cancellation, timeout, or provider failure).</summary>
    public void Abort()
    {
        if (Status == StreamingStatus.Completed)
            throw new InvalidOperationException("Cannot abort a completed streaming session.");
        Status = StreamingStatus.Aborted;
        ClosedAt = DateTime.UtcNow;
    }
}
