using ConvoLab.Infrastructure.KnowledgeStudio;
using ConvoLab.Infrastructure.PromptStudio;
using ConvoLab.Infrastructure.Simulation;
using ConvoLab.Infrastructure.WorkflowStudio;
using ConvoLab.Infrastructure.EvaluationStudio;
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

        modelBuilder.Entity<SimulationRecord>(entity =>
        {
            entity.ToTable("ConversationSimulations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Payload).IsRequired();
            entity.HasIndex(item => item.UpdatedAt);
        });

        modelBuilder.Entity<EvaluationScorecardRecord>(entity =>
        {
            entity.ToTable("EvaluationScorecards");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FailureAction).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => item.Name).IsUnique();
            entity.HasIndex(item => item.UpdatedAt);
        });
    }
}
