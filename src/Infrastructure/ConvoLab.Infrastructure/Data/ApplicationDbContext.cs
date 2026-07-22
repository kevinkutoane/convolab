using ConvoLab.Infrastructure.KnowledgeStudio;
using ConvoLab.Infrastructure.PromptStudio;
using ConvoLab.Infrastructure.Simulation;
using ConvoLab.Infrastructure.WorkflowStudio;
using ConvoLab.Infrastructure.EvaluationStudio;
using ConvoLab.Infrastructure.TraceStudio;
using ConvoLab.Infrastructure.ReplayStudio;
using ConvoLab.Infrastructure.PolicyStudio;
using ConvoLab.Infrastructure.PluginStudio;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Infrastructure.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<SimulationRecord> Simulations => Set<SimulationRecord>();
    public DbSet<KnowledgeCollectionRecord> KnowledgeCollections => Set<KnowledgeCollectionRecord>();
    public DbSet<KnowledgeDocumentRecord> KnowledgeDocuments => Set<KnowledgeDocumentRecord>();
    public DbSet<KnowledgeChunkRecord> KnowledgeChunks => Set<KnowledgeChunkRecord>();
    public DbSet<KnowledgeLifecycleRecord> KnowledgeLifecycle => Set<KnowledgeLifecycleRecord>();
    public DbSet<PromptRecord> Prompts => Set<PromptRecord>();
    public DbSet<PromptVersionRecord> PromptVersions => Set<PromptVersionRecord>();
    public DbSet<PromptLifecycleRecord> PromptLifecycle => Set<PromptLifecycleRecord>();
    public DbSet<WorkflowRecord> Workflows => Set<WorkflowRecord>();
    public DbSet<WorkflowVersionRecord> WorkflowVersions => Set<WorkflowVersionRecord>();
    public DbSet<WorkflowNodeRecord> WorkflowNodes => Set<WorkflowNodeRecord>();
    public DbSet<WorkflowTransitionRecord> WorkflowTransitions => Set<WorkflowTransitionRecord>();
    public DbSet<WorkflowAuditRecord> WorkflowAudit => Set<WorkflowAuditRecord>();
    public DbSet<EvaluationScorecardRecord> EvaluationScorecards => Set<EvaluationScorecardRecord>();
    public DbSet<EvaluationMetricDefinitionRecord> EvaluationMetricDefinitions => Set<EvaluationMetricDefinitionRecord>();
    public DbSet<EvaluationRunRecord> EvaluationRuns => Set<EvaluationRunRecord>();
    public DbSet<EvaluationMetricResultRecord> EvaluationMetricResults => Set<EvaluationMetricResultRecord>();
    public DbSet<EvaluationTestCaseRecord> EvaluationTestCases => Set<EvaluationTestCaseRecord>();
    public DbSet<EvaluationBatchRecord> EvaluationBatches => Set<EvaluationBatchRecord>();
    public DbSet<EvaluationBatchItemRecord> EvaluationBatchItems => Set<EvaluationBatchItemRecord>();
    public DbSet<TraceRecord> Traces => Set<TraceRecord>();
    public DbSet<TraceSpanRecord> TraceSpans => Set<TraceSpanRecord>();
    public DbSet<TraceEventRecord> TraceEvents => Set<TraceEventRecord>();
    public DbSet<TraceArtifactRecord> TraceArtifacts => Set<TraceArtifactRecord>();
    public DbSet<ReplayExperimentRecord> ReplayExperiments => Set<ReplayExperimentRecord>();
    public DbSet<ReplayCandidateRecord> ReplayCandidates => Set<ReplayCandidateRecord>();
    public DbSet<PolicyDefinitionRecord> PolicyDefinitions => Set<PolicyDefinitionRecord>();
    public DbSet<PolicyRuleRecord> PolicyRules => Set<PolicyRuleRecord>();
    public DbSet<PolicyDecisionRecord> PolicyDecisions => Set<PolicyDecisionRecord>();
    public DbSet<PluginRecord> Plugins => Set<PluginRecord>();
    public DbSet<PluginHealthCheckRecord> PluginHealthChecks => Set<PluginHealthCheckRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<KnowledgeCollectionRecord>(entity =>
        {
            entity.ToTable("KnowledgeCollections");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Owner).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(item => item.Name);
            entity.HasIndex(item => new { item.Status, item.UpdatedAt });
        });

        modelBuilder.Entity<KnowledgeDocumentRecord>(entity =>
        {
            entity.ToTable("KnowledgeDocuments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Title).HasMaxLength(300).IsRequired();
            entity.Property(item => item.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(160).IsRequired();
            entity.Property(item => item.StorageKey).HasMaxLength(500).IsRequired();
            entity.Property(item => item.TagsJson).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasOne<KnowledgeCollectionRecord>()
                .WithMany()
                .HasForeignKey(item => item.CollectionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.CollectionId, item.Status });
            entity.HasIndex(item => new { item.Status, item.UpdatedAt });
        });

        modelBuilder.Entity<KnowledgeChunkRecord>(entity =>
        {
            entity.ToTable("KnowledgeChunks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Text).IsRequired();
            entity.HasOne<KnowledgeDocumentRecord>()
                .WithMany()
                .HasForeignKey(item => item.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.DocumentId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.CollectionId, item.Published });
        });

        modelBuilder.Entity<KnowledgeLifecycleRecord>(entity =>
        {
            entity.ToTable("KnowledgeLifecycle");
            entity.HasKey(item => item.Id);
            entity.HasOne<KnowledgeDocumentRecord>()
                .WithMany()
                .HasForeignKey(item => item.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.DocumentId, item.At });
        });

        modelBuilder.Entity<PromptRecord>(entity =>
        {
            entity.ToTable("Prompts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Owner).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(120).IsRequired();
            entity.Property(item => item.TagsJson).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(item => item.Name);
            entity.HasIndex(item => new { item.Status, item.UpdatedAt });
        });

        modelBuilder.Entity<PromptVersionRecord>(entity =>
        {
            entity.ToTable("PromptVersions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Version).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.SectionsJson).IsRequired();
            entity.Property(item => item.VariablesJson).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasOne<PromptRecord>()
                .WithMany()
                .HasForeignKey(item => item.PromptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.PromptId, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.UpdatedAt });
        });

        modelBuilder.Entity<PromptLifecycleRecord>(entity =>
        {
            entity.ToTable("PromptLifecycle");
            entity.HasKey(item => item.Id);
            entity.HasOne<PromptVersionRecord>()
                .WithMany()
                .HasForeignKey(item => item.PromptVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.PromptVersionId, item.CreatedAt });
        });


        modelBuilder.Entity<WorkflowRecord>(entity =>
        {
            entity.ToTable("Workflows");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Owner).HasMaxLength(200).IsRequired();
            entity.Property(item => item.TagsJson).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(item => item.Name);
            entity.HasIndex(item => new { item.IsActive, item.UpdatedAt });
        });

        modelBuilder.Entity<WorkflowVersionRecord>(entity =>
        {
            entity.ToTable("WorkflowVersions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ChangeSummary).HasMaxLength(1000);
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasOne<WorkflowRecord>()
                .WithMany()
                .HasForeignKey(item => item.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.WorkflowId, item.Major, item.Minor, item.Patch }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.UpdatedAt });
        });

        modelBuilder.Entity<WorkflowNodeRecord>(entity =>
        {
            entity.ToTable("WorkflowNodes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ConfigurationJson).IsRequired();
            entity.HasOne<WorkflowVersionRecord>()
                .WithMany()
                .HasForeignKey(item => item.WorkflowVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.WorkflowVersionId, item.Kind });
        });

        modelBuilder.Entity<WorkflowTransitionRecord>(entity =>
        {
            entity.ToTable("WorkflowTransitions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Label).HasMaxLength(200);
            entity.Property(item => item.Condition).HasMaxLength(1000);
            entity.HasOne<WorkflowVersionRecord>()
                .WithMany()
                .HasForeignKey(item => item.WorkflowVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.WorkflowVersionId, item.FromNodeId });
            entity.HasIndex(item => new { item.WorkflowVersionId, item.ToNodeId });
        });

        modelBuilder.Entity<WorkflowAuditRecord>(entity =>
        {
            entity.ToTable("WorkflowAudit");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(80).IsRequired();
            entity.HasOne<WorkflowVersionRecord>()
                .WithMany()
                .HasForeignKey(item => item.WorkflowVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.WorkflowVersionId, item.CreatedAt });
        });

        modelBuilder.Entity<EvaluationScorecardRecord>(entity =>
        {
            entity.ToTable("EvaluationScorecards");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Version).HasMaxLength(50).IsRequired();
            entity.Property(item => item.FailureAction).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(item => new { item.Name, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.IsDefault });
        });

        modelBuilder.Entity<EvaluationMetricDefinitionRecord>(entity =>
        {
            entity.ToTable("EvaluationMetricDefinitions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            entity.HasOne<EvaluationScorecardRecord>().WithMany().HasForeignKey(item => item.ScorecardId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.ScorecardId, item.Key }).IsUnique();
        });

        modelBuilder.Entity<EvaluationRunRecord>(entity =>
        {
            entity.ToTable("EvaluationRuns");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SimulationTitle).HasMaxLength(240).IsRequired();
            entity.Property(item => item.ScorecardName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ScorecardVersion).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Verdict).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ReviewStatus).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Reviewer).HasMaxLength(200);
            entity.HasOne<EvaluationScorecardRecord>().WithMany().HasForeignKey(item => item.ScorecardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.SourceRunId, item.ScorecardId }).IsUnique();
            entity.HasIndex(item => new { item.Verdict, item.CreatedAt });
            entity.HasIndex(item => item.SimulationId);
        });

        modelBuilder.Entity<EvaluationMetricResultRecord>(entity =>
        {
            entity.ToTable("EvaluationMetricResults");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Detail).HasMaxLength(1000).IsRequired();
            entity.HasOne<EvaluationRunRecord>().WithMany().HasForeignKey(item => item.EvaluationRunId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.EvaluationRunId, item.Key }).IsUnique();
        });

        modelBuilder.Entity<EvaluationTestCaseRecord>(entity =>
        {
            entity.ToTable("EvaluationTestCases");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.ExpectedVerdict).HasMaxLength(50).IsRequired();
            entity.Property(item => item.TagsJson).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(item => new { item.Status, item.UpdatedAt });
            entity.HasIndex(item => item.SourceRunId);
        });

        modelBuilder.Entity<EvaluationBatchRecord>(entity =>
        {
            entity.ToTable("EvaluationBatches");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ScorecardName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.HasOne<EvaluationScorecardRecord>().WithMany().HasForeignKey(item => item.ScorecardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => item.StartedAt);
        });

        modelBuilder.Entity<EvaluationBatchItemRecord>(entity =>
        {
            entity.ToTable("EvaluationBatchItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.TestCaseName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ActualVerdict).HasMaxLength(50).IsRequired();
            entity.Property(item => item.ExpectedVerdict).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Detail).HasMaxLength(1000).IsRequired();
            entity.HasOne<EvaluationBatchRecord>().WithMany().HasForeignKey(item => item.BatchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.BatchId, item.TestCaseId }).IsUnique();
        });

        modelBuilder.Entity<TraceRecord>(entity =>
        {
            entity.ToTable("Traces");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OperationName).HasMaxLength(240).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.SimulationTitle).HasMaxLength(240);
            entity.Property(item => item.Provider).HasMaxLength(160);
            entity.Property(item => item.Model).HasMaxLength(200);
            entity.Property(item => item.Workflow).HasMaxLength(300);
            entity.Property(item => item.PromptVersion).HasMaxLength(160);
            entity.Property(item => item.KnowledgeCollection).HasMaxLength(200);
            entity.Property(item => item.EvaluationVerdict).HasMaxLength(50);
            entity.Property(item => item.Currency).HasMaxLength(10).IsRequired();
            entity.HasIndex(item => item.CorrelationId);
            entity.HasIndex(item => item.SourceRunId).IsUnique();
            entity.HasIndex(item => new { item.Status, item.StartedAt });
            entity.HasIndex(item => item.SimulationId);
            entity.HasIndex(item => item.Provider);
        });

        modelBuilder.Entity<TraceSpanRecord>(entity =>
        {
            entity.ToTable("TraceSpans");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(240).IsRequired();
            entity.Property(item => item.Capability).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Detail).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.AttributesJson).IsRequired();
            entity.HasOne<TraceRecord>().WithMany().HasForeignKey(item => item.TraceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.TraceId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.Capability, item.Status });
            entity.HasIndex(item => item.ParentSpanId);
        });

        modelBuilder.Entity<TraceEventRecord>(entity =>
        {
            entity.ToTable("TraceEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Level).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(4000).IsRequired();
            entity.Property(item => item.AttributesJson).IsRequired();
            entity.HasOne<TraceRecord>().WithMany().HasForeignKey(item => item.TraceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.TraceId, item.OccurredAt });
            entity.HasIndex(item => item.SpanId);
        });

        modelBuilder.Entity<TraceArtifactRecord>(entity =>
        {
            entity.ToTable("TraceArtifacts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(240).IsRequired();
            entity.Property(item => item.ContentType).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Content).IsRequired();
            entity.HasOne<TraceRecord>().WithMany().HasForeignKey(item => item.TraceId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.TraceId, item.Kind });
            entity.HasIndex(item => item.SpanId);
        });

        modelBuilder.Entity<ReplayExperimentRecord>(entity =>
        {
            entity.ToTable("ReplayExperiments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.HasIndex(item => new { item.SimulationId, item.SourceRunId });
            entity.HasIndex(item => new { item.Status, item.UpdatedAt });
        });

        modelBuilder.Entity<ReplayCandidateRecord>(entity =>
        {
            entity.ToTable("ReplayCandidates");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Label).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Workflow).HasMaxLength(300).IsRequired();
            entity.Property(item => item.PromptVersion).HasMaxLength(200).IsRequired();
            entity.Property(item => item.KnowledgeCollection).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Provider).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Model).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Mode).HasMaxLength(50).IsRequired();
            entity.HasOne<ReplayExperimentRecord>().WithMany().HasForeignKey(item => item.ExperimentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => item.RunId).IsUnique();
            entity.HasIndex(item => new { item.ExperimentId, item.CreatedAt });
        });

        modelBuilder.Entity<PolicyDefinitionRecord>(entity =>
        {
            entity.ToTable("PolicyDefinitions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Owner).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Domain).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Scope).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Environment).HasMaxLength(100).IsRequired();
            entity.Property(item => item.DefaultEffect).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(item => new { item.PolicyKey, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.Domain, item.Status, item.Environment });
            entity.HasIndex(item => item.TenantId);
        });

        modelBuilder.Entity<PolicyRuleRecord>(entity =>
        {
            entity.ToTable("PolicyRules");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Effect).HasMaxLength(50).IsRequired();
            entity.Property(item => item.MatchJson).IsRequired();
            entity.Property(item => item.ConstraintsJson).IsRequired();
            entity.HasOne<PolicyDefinitionRecord>().WithMany().HasForeignKey(item => item.PolicyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.PolicyId, item.Name }).IsUnique();
            entity.HasIndex(item => new { item.PolicyId, item.Priority });
        });

        modelBuilder.Entity<PolicyDecisionRecord>(entity =>
        {
            entity.ToTable("PolicyDecisions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PolicyName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Domain).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Effect).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.ContextJson).IsRequired();
            entity.Property(item => item.ConstraintsJson).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(100).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
            entity.HasOne<PolicyDefinitionRecord>().WithMany().HasForeignKey(item => item.PolicyId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(item => new { item.Domain, item.Effect, item.CreatedAt });
            entity.HasIndex(item => item.CorrelationId);
            entity.HasIndex(item => item.SimulationId);
            entity.HasIndex(item => item.RunId);
        });

        modelBuilder.Entity<PluginRecord>(entity =>
        {
            entity.ToTable("Plugins");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Key).HasMaxLength(160).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Publisher).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Version).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Category).HasMaxLength(80).IsRequired();
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.HealthStatus).HasMaxLength(50).IsRequired();
            entity.Property(item => item.HealthMessage).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.ManifestUrl).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.EntryPoint).HasMaxLength(500).IsRequired();
            entity.Property(item => item.PlatformApiVersion).HasMaxLength(50).IsRequired();
            entity.Property(item => item.CapabilitiesJson).IsRequired();
            entity.Property(item => item.PermissionsJson).IsRequired();
            entity.Property(item => item.ConfigurationSchema).IsRequired();
            entity.Property(item => item.MetadataJson).IsRequired();
            entity.Property(item => item.Revision).IsConcurrencyToken();
            entity.HasIndex(item => new { item.PluginKey, item.Version }).IsUnique();
            entity.HasIndex(item => item.PluginKey)
                .IsUnique()
                .HasFilter("\"Status\" = 'Active'");
            entity.HasIndex(item => new { item.Key, item.Status });
            entity.HasIndex(item => new { item.Category, item.HealthStatus });
        });

        modelBuilder.Entity<PluginHealthCheckRecord>(entity =>
        {
            entity.ToTable("PluginHealthChecks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Status).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Message).HasMaxLength(2000).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(80).IsRequired();
            entity.HasOne<PluginRecord>().WithMany().HasForeignKey(item => item.PluginId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(item => new { item.PluginId, item.CheckedAt });
            entity.HasIndex(item => new { item.Status, item.CheckedAt });
        });

        modelBuilder.Entity<SimulationRecord>(entity =>
        {
            entity.ToTable("ConversationSimulations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Payload).IsRequired();
            entity.HasIndex(item => item.UpdatedAt);
        });
    }
}
