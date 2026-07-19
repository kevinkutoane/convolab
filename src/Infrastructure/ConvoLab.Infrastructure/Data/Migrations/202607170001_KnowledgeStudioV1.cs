using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace ConvoLab.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("202607170001_KnowledgeStudioV1")]
public partial class KnowledgeStudioV1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name:"KnowledgeCollections",columns:t=>new{Id=t.Column<Guid>(nullable:false),Name=t.Column<string>(nullable:false),Description=t.Column<string>(nullable:false),Owner=t.Column<string>(nullable:false),Classification=t.Column<int>(nullable:false),Status=t.Column<int>(nullable:false),CreatedAt=t.Column<DateTimeOffset>(nullable:false),UpdatedAt=t.Column<DateTimeOffset>(nullable:false)},constraints:t=>t.PrimaryKey("PK_KnowledgeCollections",x=>x.Id));
        migrationBuilder.CreateTable(name:"KnowledgeDocuments",columns:t=>new{Id=t.Column<Guid>(nullable:false),CollectionId=t.Column<Guid>(nullable:false),Title=t.Column<string>(nullable:false),OriginalFileName=t.Column<string>(nullable:false),ContentType=t.Column<string>(nullable:false),SizeBytes=t.Column<long>(nullable:false),StorageKey=t.Column<string>(nullable:false),Status=t.Column<int>(nullable:false),Classification=t.Column<int>(nullable:false),Owner=t.Column<string>(nullable:false),Category=t.Column<string>(nullable:false),TagsJson=t.Column<string>(nullable:false),Version=t.Column<int>(nullable:false),Error=t.Column<string>(nullable:true),CreatedAt=t.Column<DateTimeOffset>(nullable:false),UpdatedAt=t.Column<DateTimeOffset>(nullable:false),PublishedAt=t.Column<DateTimeOffset>(nullable:true)},constraints:t=>{t.PrimaryKey("PK_KnowledgeDocuments",x=>x.Id);t.ForeignKey("FK_KnowledgeDocuments_KnowledgeCollections_CollectionId",x=>x.CollectionId,"KnowledgeCollections","Id",onDelete:ReferentialAction.Cascade);});
        migrationBuilder.CreateTable(name:"KnowledgeChunks",columns:t=>new{Id=t.Column<Guid>(nullable:false),DocumentId=t.Column<Guid>(nullable:false),CollectionId=t.Column<Guid>(nullable:false),Sequence=t.Column<int>(nullable:false),Text=t.Column<string>(nullable:false),PageNumber=t.Column<int>(nullable:true),Section=t.Column<string>(nullable:true),CharacterCount=t.Column<int>(nullable:false),EstimatedTokens=t.Column<int>(nullable:false),Classification=t.Column<int>(nullable:false),Published=t.Column<bool>(nullable:false)},constraints:t=>{t.PrimaryKey("PK_KnowledgeChunks",x=>x.Id);t.ForeignKey("FK_KnowledgeChunks_KnowledgeDocuments_DocumentId",x=>x.DocumentId,"KnowledgeDocuments","Id",onDelete:ReferentialAction.Cascade);});
        migrationBuilder.CreateTable(name:"KnowledgeLifecycle",columns:t=>new{Id=t.Column<Guid>(nullable:false),DocumentId=t.Column<Guid>(nullable:false),Actor=t.Column<string>(nullable:false),Action=t.Column<string>(nullable:false),Reason=t.Column<string>(nullable:true),PreviousStatus=t.Column<int>(nullable:false),NewStatus=t.Column<int>(nullable:false),At=t.Column<DateTimeOffset>(nullable:false)},constraints:t=>{t.PrimaryKey("PK_KnowledgeLifecycle",x=>x.Id);t.ForeignKey("FK_KnowledgeLifecycle_KnowledgeDocuments_DocumentId",x=>x.DocumentId,"KnowledgeDocuments","Id",onDelete:ReferentialAction.Cascade);});
        migrationBuilder.CreateIndex(name:"IX_KnowledgeCollections_Name",table:"KnowledgeCollections",column:"Name");
        migrationBuilder.CreateIndex(name:"IX_KnowledgeDocuments_CollectionId_Status",table:"KnowledgeDocuments",columns:new[]{"CollectionId","Status"});
        migrationBuilder.CreateIndex(name:"IX_KnowledgeChunks_CollectionId_Published",table:"KnowledgeChunks",columns:new[]{"CollectionId","Published"});
        migrationBuilder.CreateIndex(name:"IX_KnowledgeLifecycle_DocumentId",table:"KnowledgeLifecycle",column:"DocumentId");
    }
    protected override void Down(MigrationBuilder migrationBuilder){migrationBuilder.DropTable("KnowledgeLifecycle");migrationBuilder.DropTable("KnowledgeChunks");migrationBuilder.DropTable("KnowledgeDocuments");migrationBuilder.DropTable("KnowledgeCollections");}
}
