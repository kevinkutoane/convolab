using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607220002_TraceStudioV1")]
public partial class TraceStudioV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Traces",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CorrelationId = table.Column<Guid>(nullable: false),
                OperationName = table.Column<string>(maxLength: 240, nullable: false),
                Source = table.Column<string>(maxLength: 80, nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                SimulationId = table.Column<Guid>(nullable: true),
                SimulationTitle = table.Column<string>(maxLength: 240, nullable: true),
                SourceRunId = table.Column<Guid>(nullable: true),
                ReplayedFromRunId = table.Column<Guid>(nullable: true),
                Provider = table.Column<string>(maxLength: 160, nullable: true),
                Model = table.Column<string>(maxLength: 200, nullable: true),
                Workflow = table.Column<string>(maxLength: 300, nullable: true),
                PromptVersion = table.Column<string>(maxLength: 160, nullable: true),
                KnowledgeCollection = table.Column<string>(maxLength: 200, nullable: true),
                EvaluationVerdict = table.Column<string>(maxLength: 50, nullable: true),
                DurationMs = table.Column<double>(nullable: false),
                TotalTokens = table.Column<int>(nullable: false),
                ActualCost = table.Column<decimal>(nullable: false),
                Currency = table.Column<string>(maxLength: 10, nullable: false),
                FailureReason = table.Column<string>(nullable: true),
                StartedAt = table.Column<DateTimeOffset>(nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Traces", item => item.Id));

        migrationBuilder.CreateTable(
            name: "TraceSpans",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TraceId = table.Column<Guid>(nullable: false),
                ParentSpanId = table.Column<Guid>(nullable: true),
                Name = table.Column<string>(maxLength: 240, nullable: false),
                Capability = table.Column<string>(maxLength: 100, nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false),
                Detail = table.Column<string>(maxLength: 4000, nullable: false),
                Sequence = table.Column<int>(nullable: false),
                StartedAt = table.Column<DateTimeOffset>(nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(nullable: true),
                DurationMs = table.Column<double>(nullable: false),
                AttributesJson = table.Column<string>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TraceSpans", item => item.Id);
                table.ForeignKey(
                    name: "FK_TraceSpans_Traces_TraceId",
                    column: item => item.TraceId,
                    principalTable: "Traces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TraceEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TraceId = table.Column<Guid>(nullable: false),
                SpanId = table.Column<Guid>(nullable: true),
                Name = table.Column<string>(maxLength: 160, nullable: false),
                Level = table.Column<string>(maxLength: 50, nullable: false),
                Message = table.Column<string>(maxLength: 4000, nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(nullable: false),
                AttributesJson = table.Column<string>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TraceEvents", item => item.Id);
                table.ForeignKey(
                    name: "FK_TraceEvents_Traces_TraceId",
                    column: item => item.TraceId,
                    principalTable: "Traces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TraceArtifacts",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                TraceId = table.Column<Guid>(nullable: false),
                SpanId = table.Column<Guid>(nullable: true),
                Kind = table.Column<string>(maxLength: 100, nullable: false),
                Name = table.Column<string>(maxLength: 240, nullable: false),
                ContentType = table.Column<string>(maxLength: 160, nullable: false),
                Content = table.Column<string>(nullable: false),
                IsSensitive = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TraceArtifacts", item => item.Id);
                table.ForeignKey(
                    name: "FK_TraceArtifacts_Traces_TraceId",
                    column: item => item.TraceId,
                    principalTable: "Traces",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_Traces_CorrelationId", table: "Traces", column: "CorrelationId");
        migrationBuilder.CreateIndex(name: "IX_Traces_SourceRunId", table: "Traces", column: "SourceRunId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_Traces_Status_StartedAt", table: "Traces", columns: new[] { "Status", "StartedAt" });
        migrationBuilder.CreateIndex(name: "IX_Traces_SimulationId", table: "Traces", column: "SimulationId");
        migrationBuilder.CreateIndex(name: "IX_Traces_Provider", table: "Traces", column: "Provider");
        migrationBuilder.CreateIndex(name: "IX_TraceSpans_TraceId_Sequence", table: "TraceSpans", columns: new[] { "TraceId", "Sequence" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_TraceSpans_Capability_Status", table: "TraceSpans", columns: new[] { "Capability", "Status" });
        migrationBuilder.CreateIndex(name: "IX_TraceSpans_ParentSpanId", table: "TraceSpans", column: "ParentSpanId");
        migrationBuilder.CreateIndex(name: "IX_TraceEvents_TraceId_OccurredAt", table: "TraceEvents", columns: new[] { "TraceId", "OccurredAt" });
        migrationBuilder.CreateIndex(name: "IX_TraceEvents_SpanId", table: "TraceEvents", column: "SpanId");
        migrationBuilder.CreateIndex(name: "IX_TraceArtifacts_TraceId_Kind", table: "TraceArtifacts", columns: new[] { "TraceId", "Kind" });
        migrationBuilder.CreateIndex(name: "IX_TraceArtifacts_SpanId", table: "TraceArtifacts", column: "SpanId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "TraceArtifacts");
        migrationBuilder.DropTable(name: "TraceEvents");
        migrationBuilder.DropTable(name: "TraceSpans");
        migrationBuilder.DropTable(name: "Traces");
    }
}
