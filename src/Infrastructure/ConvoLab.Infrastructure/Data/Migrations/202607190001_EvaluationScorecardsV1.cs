using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607190001_EvaluationScorecardsV1")]
public partial class EvaluationScorecardsV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EvaluationScorecards",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 120, nullable: false),
                Description = table.Column<string>(maxLength: 500, nullable: false),
                MinimumGroundedness = table.Column<double>(nullable: false),
                MinimumRelevance = table.Column<double>(nullable: false),
                MinimumSafety = table.Column<double>(nullable: false),
                MinimumOverallScore = table.Column<double>(nullable: false),
                FailureAction = table.Column<string>(maxLength: 80, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_EvaluationScorecards", item => item.Id));

        migrationBuilder.CreateIndex(
            name: "IX_EvaluationScorecards_Name",
            table: "EvaluationScorecards",
            column: "Name",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_EvaluationScorecards_UpdatedAt",
            table: "EvaluationScorecards",
            column: "UpdatedAt");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("EvaluationScorecards");
}
