using ConvoLab.Application.Common.Errors;
using ConvoLab.Application.Common.Persistence;
using ConvoLab.Application.PromptStudio;
using ConvoLab.Domain.Prompt.Enums;

namespace ConvoLab.Application.Tests.PromptStudio;

public sealed class PromptStudioServiceTests
{
    [Fact]
    public async Task Published_Version_Becomes_Runtime_Artifact()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptStudioService(repository, new FakeUnitOfWork());
        var prompt = await service.CreateAsync(new CreatePromptCommand(
            "Claims Assistant", "Claims prompt", "Kevin", "Claims", ["claims"]));
        var version = await service.CreateVersionAsync(prompt.Id, VersionCommand("1.0.0"));

        version = (await service.TransitionAsync(version.Id, "submit", Command(version.Revision)))!;
        version = (await service.TransitionAsync(version.Id, "approve", Command(version.Revision)))!;
        version = (await service.TransitionAsync(version.Id, "publish", Command(version.Revision)))!;

        Assert.Equal(PromptStudioStatus.Published, version.Status);
        var published = await service.ListPublishedAsync();
        Assert.Single(published);
        Assert.Equal("Claims Assistant v1.0.0", published[0].DisplayName);
    }

    [Fact]
    public async Task Duplicate_Semantic_Version_Is_Rejected()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptStudioService(repository, new FakeUnitOfWork());
        var prompt = await service.CreateAsync(new CreatePromptCommand("Prompt", "", "Kevin", "General", []));
        await service.CreateVersionAsync(prompt.Id, VersionCommand("1.0.0"));

        var error = await Assert.ThrowsAsync<ResourceConflictException>(() =>
            service.CreateVersionAsync(prompt.Id, VersionCommand("1.0.0")));
        Assert.Equal("prompt.version.duplicate", error.Code);
    }

    [Fact]
    public async Task Preview_Reports_Missing_Required_Variables()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptStudioService(repository, new FakeUnitOfWork());
        var prompt = await service.CreateAsync(new CreatePromptCommand("Prompt", "", "Kevin", "General", []));
        var version = await service.CreateVersionAsync(prompt.Id, VersionCommand("1.0.0"));

        var rendered = await service.RenderAsync(new RenderPromptCommand(
            version.Id,
            new Dictionary<string, string> { ["customerMessage"] = "Hello" }));

        Assert.Contains("knowledgePackage", rendered.MissingVariables);
        Assert.Contains("Hello", rendered.RenderedText);
    }


    [Fact]
    public async Task Publishing_A_New_Version_Deprecates_The_Previous_Active_Version()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptStudioService(repository, new FakeUnitOfWork());
        var prompt = await service.CreateAsync(new CreatePromptCommand("Claims", "", "Kevin", "Claims", []));
        var first = await service.CreateVersionAsync(prompt.Id, VersionCommand("1.0.0"));
        first = (await service.TransitionAsync(first.Id, "submit", Command(first.Revision)))!;
        first = (await service.TransitionAsync(first.Id, "approve", Command(first.Revision)))!;
        first = (await service.TransitionAsync(first.Id, "publish", Command(first.Revision)))!;

        var refreshedPrompt = (await service.GetAsync(prompt.Id))!;
        var second = await service.CreateVersionAsync(
            prompt.Id,
            VersionCommand("1.1.0") with { ExpectedPromptRevision = refreshedPrompt.Revision });
        second = (await service.TransitionAsync(second.Id, "submit", Command(second.Revision)))!;
        second = (await service.TransitionAsync(second.Id, "approve", Command(second.Revision)))!;
        second = (await service.TransitionAsync(second.Id, "publish", Command(second.Revision)))!;

        var detail = (await service.GetAsync(prompt.Id))!;
        Assert.Equal(PromptStudioStatus.Deprecated, detail.Versions.Single(item => item.Id == first.Id).Status);
        Assert.Equal(PromptStudioStatus.Published, detail.Versions.Single(item => item.Id == second.Id).Status);
        Assert.Single(await service.ListPublishedAsync());
    }

    [Fact]
    public async Task Stale_Prompt_Version_Transition_Is_Rejected()
    {
        var repository = new InMemoryPromptRepository();
        var service = new PromptStudioService(repository, new FakeUnitOfWork());
        var prompt = await service.CreateAsync(new CreatePromptCommand("Prompt", "", "Kevin", "General", []));
        var version = await service.CreateVersionAsync(prompt.Id, VersionCommand("1.0.0"));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            service.TransitionAsync(version.Id, "submit", Command(version.Revision + 10)));
    }

    private static CreatePromptVersionCommand VersionCommand(string version)
        => new(version, "Initial", [
            new PromptSectionInput(PromptSectionKind.System, "System", "Use {{knowledgePackage}}.", 10),
            new PromptSectionInput(PromptSectionKind.User, "User", "{{customerMessage}}", 20)
        ]);

    private static PromptLifecycleCommand Command(long revision)
        => new("reviewer", "test", revision);

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class InMemoryPromptRepository : IPromptStudioRepository
    {
        private readonly Dictionary<Guid, PromptDefinitionState> _prompts = [];
        private readonly Dictionary<Guid, PromptVersionState> _versions = [];
        public List<PromptLifecycleState> Lifecycle { get; } = [];

        public Task<IReadOnlyList<PromptDefinitionState>> ListPromptsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PromptDefinitionState>>(_prompts.Values.ToList());
        public Task<PromptDefinitionState?> GetPromptAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_prompts.GetValueOrDefault(id));
        public Task<IReadOnlyList<PromptVersionState>> ListVersionsAsync(Guid promptId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PromptVersionState>>(_versions.Values.Where(x => x.PromptId == promptId).ToList());
        public Task<PromptVersionState?> GetVersionAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_versions.GetValueOrDefault(id));
        public Task<bool> VersionExistsAsync(Guid promptId, string semanticVersion, CancellationToken ct = default)
            => Task.FromResult(_versions.Values.Any(x => x.PromptId == promptId && x.Version == semanticVersion));
        public Task<IReadOnlyList<PromptVersionState>> ListPublishedVersionsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PromptVersionState>>(_versions.Values.Where(x => x.Status == PromptStatus.Active).ToList());
        public Task AddPromptAsync(PromptDefinitionState prompt, CancellationToken ct = default) { _prompts.Add(prompt.Id, prompt); return Task.CompletedTask; }
        public Task UpdatePromptAsync(PromptDefinitionState prompt, long expectedRevision, CancellationToken ct = default)
        {
            if (_prompts[prompt.Id].Revision != expectedRevision) throw new ConcurrencyConflictException("prompt", prompt.Id);
            _prompts[prompt.Id] = prompt;
            return Task.CompletedTask;
        }
        public Task AddVersionAsync(PromptVersionState version, CancellationToken ct = default) { _versions.Add(version.Id, version); return Task.CompletedTask; }
        public Task UpdateVersionAsync(PromptVersionState version, long expectedRevision, CancellationToken ct = default)
        {
            if (_versions[version.Id].Revision != expectedRevision) throw new ConcurrencyConflictException("prompt version", version.Id);
            _versions[version.Id] = version;
            return Task.CompletedTask;
        }
        public Task AddLifecycleEntryAsync(PromptLifecycleState entry, CancellationToken ct = default) { Lifecycle.Add(entry); return Task.CompletedTask; }
    }
}
