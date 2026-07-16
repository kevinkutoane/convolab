using ConvoLab.Domain.Knowledge.Aggregates;
using ConvoLab.Domain.Knowledge.Enums;
using ConvoLab.Domain.Knowledge.Events;
using ConvoLab.Domain.Knowledge.ValueObjects;
using Xunit;

namespace ConvoLab.Domain.Tests.Knowledge;

public class KnowledgeSourceTests
{
    private readonly KnowledgeOwner _owner = KnowledgeOwner.Create(Guid.NewGuid(), "Platform Team", "Engineering");
    private readonly KnowledgePolicy _policy = KnowledgePolicy.Default();

    [Fact]
    public void Register_Should_Initialize_Source_With_Draft_Status()
    {
        var source = KnowledgeSource.Register("SharePoint HR", KnowledgeSourceType.SharePoint, _owner, _policy);

        Assert.Equal("SharePoint HR", source.Name);
        Assert.Equal(KnowledgeSourceType.SharePoint, source.SourceType);
        Assert.Equal(KnowledgeLifecycleStatus.Draft, source.Status);
        Assert.Contains(source.DomainEvents, e => e is KnowledgeSourceRegisteredEvent);
    }

    [Fact]
    public void IngestDocument_Should_Add_Document_And_Inherit_Policy()
    {
        var source = KnowledgeSource.Register("API Docs", KnowledgeSourceType.RestApi, _owner, _policy);

        var doc = source.IngestDocument("Auth Guide", "api-auth-01", "https://api.internal/auth");

        Assert.Single(source.Documents);
        Assert.Equal("Auth Guide", doc.Title);
        Assert.Equal("api-auth-01", doc.Reference.ExternalId);
        Assert.Equal(_policy, doc.Policy);
        Assert.Equal(KnowledgeLifecycleStatus.Draft, doc.Status);
    }

    [Fact]
    public void Document_Publishing_Requires_Approval_If_Policy_Dictates()
    {
        var strictPolicy = KnowledgePolicy.Create("Strict", KnowledgeClassification.Confidential, requiresApprovalBeforePublish: true);
        var source = KnowledgeSource.Register("Vault", KnowledgeSourceType.FileSystem, _owner, strictPolicy);
        var doc = source.IngestDocument("Secret", "sec-01", "file://sec-01");

        // Needs at least one chunk to be published
        doc.AddChunk(ChunkType.Document, "Secret content");

        // Attempting to publish while Draft should fail because policy requires approval
        Assert.Throws<InvalidOperationException>(() => source.PublishDocument(doc.Id));

        // Walk the lifecycle
        doc.SubmitForApproval();
        doc.Approve();
        source.PublishDocument(doc.Id);

        Assert.Equal(KnowledgeLifecycleStatus.Published, doc.Status);
        Assert.Contains(source.DomainEvents, e => e is KnowledgeVersionPublishedEvent);
    }

    [Fact]
    public void Published_Document_Cannot_Be_Modified_Directly()
    {
        var source = KnowledgeSource.Register("Wiki", KnowledgeSourceType.Confluence, _owner, KnowledgePolicy.Default());
        var doc = source.IngestDocument("Page 1", "pg-1", "url");
        doc.AddChunk(ChunkType.Paragraph, "Content");
        source.PublishDocument(doc.Id);

        // Modifying chunks of a published document is forbidden
        Assert.Throws<InvalidOperationException>(() => doc.AddChunk(ChunkType.Paragraph, "More content"));
    }

    [Fact]
    public void RegisterContentChange_Should_Create_New_Draft_Version()
    {
        var source = KnowledgeSource.Register("Wiki", KnowledgeSourceType.Confluence, _owner, KnowledgePolicy.Default());
        var doc = source.IngestDocument("Page 1", "pg-1", "url");
        doc.AddChunk(ChunkType.Paragraph, "Content");
        source.PublishDocument(doc.Id);

        var v1 = doc.Version;

        // Content changes at source
        source.RegisterContentChange(doc.Id, "new-hash");

        Assert.Equal(KnowledgeLifecycleStatus.Draft, doc.Status);
        Assert.True(doc.Version.IsNewerThan(v1));
        Assert.Empty(doc.Chunks); // Needs re-chunking
        Assert.Contains(source.DomainEvents, e => e is KnowledgeUpdatedEvent);
    }
}
