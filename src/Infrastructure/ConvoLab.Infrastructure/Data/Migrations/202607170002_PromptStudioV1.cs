using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607170002_PromptStudioV1")]
public partial class PromptStudioV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name:"Prompts",columns:table=>new{Id=table.Column<Guid>(nullable:false),Name=table.Column<string>(nullable:false),Description=table.Column<string>(nullable:false),Owner=table.Column<string>(nullable:false),Category=table.Column<string>(nullable:false),TagsJson=table.Column<string>(nullable:false),Status=table.Column<string>(nullable:false),CreatedAt=table.Column<DateTimeOffset>(nullable:false),UpdatedAt=table.Column<DateTimeOffset>(nullable:false)},constraints:table=>table.PrimaryKey("PK_Prompts",x=>x.Id));
        migrationBuilder.CreateTable(name:"PromptVersions",columns:table=>new{Id=table.Column<Guid>(nullable:false),PromptId=table.Column<Guid>(nullable:false),Version=table.Column<string>(nullable:false),Status=table.Column<string>(nullable:false),ChangeSummary=table.Column<string>(nullable:false),SectionsJson=table.Column<string>(nullable:false),VariablesJson=table.Column<string>(nullable:false),EstimatedTokens=table.Column<int>(nullable:false),CreatedAt=table.Column<DateTimeOffset>(nullable:false),UpdatedAt=table.Column<DateTimeOffset>(nullable:false),PublishedAt=table.Column<DateTimeOffset>(nullable:true)},constraints:table=>{table.PrimaryKey("PK_PromptVersions",x=>x.Id);table.ForeignKey("FK_PromptVersions_Prompts_PromptId",x=>x.PromptId,"Prompts","Id",onDelete:ReferentialAction.Cascade);});
        migrationBuilder.CreateTable(name:"PromptLifecycle",columns:table=>new{Id=table.Column<Guid>(nullable:false),PromptVersionId=table.Column<Guid>(nullable:false),Actor=table.Column<string>(nullable:false),Action=table.Column<string>(nullable:false),Reason=table.Column<string>(nullable:true),PreviousStatus=table.Column<string>(nullable:false),NewStatus=table.Column<string>(nullable:false),CreatedAt=table.Column<DateTimeOffset>(nullable:false)},constraints:table=>{table.PrimaryKey("PK_PromptLifecycle",x=>x.Id);table.ForeignKey("FK_PromptLifecycle_PromptVersions_PromptVersionId",x=>x.PromptVersionId,"PromptVersions","Id",onDelete:ReferentialAction.Cascade);});
        migrationBuilder.CreateIndex(name:"IX_Prompts_Name",table:"Prompts",column:"Name");
        migrationBuilder.CreateIndex(name:"IX_PromptVersions_PromptId_Version",table:"PromptVersions",columns:new[]{"PromptId","Version"},unique:true);
        migrationBuilder.CreateIndex(name:"IX_PromptLifecycle_PromptVersionId",table:"PromptLifecycle",column:"PromptVersionId");
    }
    protected override void Down(MigrationBuilder migrationBuilder){migrationBuilder.DropTable("PromptLifecycle");migrationBuilder.DropTable("PromptVersions");migrationBuilder.DropTable("Prompts");}
}
