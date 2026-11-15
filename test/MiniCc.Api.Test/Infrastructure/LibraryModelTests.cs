using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared.Data;

namespace MiniCc.Api.Test.Infrastructure;

public class LibraryModelTests
{
    [Fact]
    public void Migration_BackfillsLegacyReadingAndExtensionState()
    {
        using var db = new MiniCcDbContext(new DbContextOptionsBuilder<MiniCcDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").EnableServiceProviderCaching(false).Options, Mock.Of<IEncryptionService>());
        var script = db.GetService<IMigrator>().GenerateScript("20250719091116_ApiKey");
        script.Should().Contain("\"ReadingStatus\" = CASE WHEN \"ReadAt\" IS NOT NULL THEN 2 ELSE 0 END");
        script.Should().Contain("\"SavedFromExtensionAt\" = \"CreatedAt\"");
    }

    [Fact]
    public void Model_DefinesSoftDeletionAndSummarySearch()
    {
        using var db = new MiniCcDbContext(new DbContextOptionsBuilder<MiniCcDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").EnableServiceProviderCaching(false).Options, Mock.Of<IEncryptionService>());
        var article = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Article))!;
        article.GetQueryFilter().Should().NotBeNull();
        db.Model.FindEntityType(typeof(Highlight))!.GetQueryFilter().Should().NotBeNull();
        article.FindProperty(nameof(Article.SearchVector))!.GetTsVectorProperties().Should().Contain("Summary");
        article.FindProperty(nameof(Article.NormalizedUrl))!.GetColumnType().Should().Be("text");
        article.FindProperty(nameof(Article.NormalizedUrl))!.GetMaxLength().Should().BeNull();
        article.FindNavigation(nameof(Article.Url))!.TargetEntityType.FindProperty("Value")!.GetColumnType().Should().Be("text");
        article.GetIndexes().Single(i => i.GetDatabaseName() == "IX_Articles_NormalizedUrl").GetMethod().Should().Be("hash");
        article.FindProperty(nameof(Article.ProgressSessionId))!.GetMaxLength().Should().Be(64);
        article.GetIndexes().Select(i => i.GetDatabaseName()).Should().Contain(new[]
        {
            "IX_Articles_DeletedAt_IsArchived_CreatedAt_Id", "IX_Articles_LastReadAt",
            "IX_Articles_PublishedAt", "IX_Articles_NormalizedUrl", "IX_Articles_DeletedAt"
        });
    }
}
