using ConvoLab.Domain.Common;

namespace ConvoLab.Domain.Intelligence.ValueObjects;

/// <summary>A single ordered chunk emitted during a streaming session.</summary>
public class StreamingChunk : ValueObject
{
    public int Sequence { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime EmittedAt { get; private set; }

    private StreamingChunk() { } // For EF Core

    private StreamingChunk(int sequence, string content, DateTime emittedAt)
    {
        if (sequence < 0) throw new ArgumentException("Sequence cannot be negative.");
        Sequence = sequence;
        Content = content ?? string.Empty;
        EmittedAt = emittedAt;
    }

    public static StreamingChunk Create(int sequence, string content) => new(sequence, content, DateTime.UtcNow);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Sequence;
        yield return Content;
        yield return EmittedAt;
    }
}

/// <summary>
/// Statistics for a streaming session: chunk count, first-token latency, and
/// total streaming duration. First-token latency is the metric conversational
/// UX lives and dies by.
/// </summary>
public class StreamingStatistics : ValueObject
{
    public int ChunkCount { get; private set; }
    public TimeSpan TimeToFirstChunk { get; private set; }
    public TimeSpan TotalDuration { get; private set; }
    public int TotalCharacters { get; private set; }

    private StreamingStatistics() { } // For EF Core

    private StreamingStatistics(int chunkCount, TimeSpan firstChunk, TimeSpan total, int characters)
    {
        if (chunkCount < 0 || characters < 0) throw new ArgumentException("Counts cannot be negative.");
        ChunkCount = chunkCount;
        TimeToFirstChunk = firstChunk;
        TotalDuration = total;
        TotalCharacters = characters;
    }

    public static StreamingStatistics Create(int chunkCount, TimeSpan timeToFirstChunk, TimeSpan totalDuration, int totalCharacters)
        => new(chunkCount, timeToFirstChunk, totalDuration, totalCharacters);

    public static StreamingStatistics Empty() => new(0, TimeSpan.Zero, TimeSpan.Zero, 0);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ChunkCount;
        yield return TimeToFirstChunk;
        yield return TotalDuration;
        yield return TotalCharacters;
    }
}

/// <summary>The terminal record of a streaming session.</summary>
public class StreamingCompletion : ValueObject
{
    public string AssembledContent { get; private set; } = string.Empty;
    public StreamingStatistics Statistics { get; private set; } = StreamingStatistics.Empty();
    public DateTime CompletedAt { get; private set; }

    private StreamingCompletion() { } // For EF Core

    private StreamingCompletion(string content, StreamingStatistics statistics, DateTime completedAt)
    {
        AssembledContent = content ?? string.Empty;
        Statistics = statistics;
        CompletedAt = completedAt;
    }

    public static StreamingCompletion Create(string assembledContent, StreamingStatistics statistics)
        => new(assembledContent, statistics, DateTime.UtcNow);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return AssembledContent;
        yield return Statistics;
        yield return CompletedAt;
    }
}
