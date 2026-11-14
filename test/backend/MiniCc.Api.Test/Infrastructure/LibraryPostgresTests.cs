using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Infrastructure.Repositories;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;
using Npgsql;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class LibraryPostgresTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SoftDeletion_FiltersArticlesHighlightsAndTagCountsAndRestoresRelationships()
    {
        var tag = Tag.Create(Guid.NewGuid().ToString());
        var article = await fixture.SeedArticleAsync(a =>
        {
            a.AddTag(tag);
            a.AddHighlight(Highlight.Create(a.Id, "text", "note", 0, 4));
            a.SetFavorite(true);
            a.SetArchived(true);
            a.MarkRead(DateTimeOffset.UtcNow);
            a.ApplyProgress(new ReadingPosition(10, 1, .3, 40), "session", DateTimeOffset.UtcNow);
            a.MoveToTrash(DateTimeOffset.UtcNow);
        });
        await using var db = fixture.CreateContext();
        (await db.Articles.AnyAsync(a => a.Id == article.Id)).Should().BeFalse();
        (await db.Highlights.AnyAsync(h => h.ArticleId == article.Id)).Should().BeFalse();
        (await db.Tags.Where(t => t.Id == tag.Id).Select(t => t.Articles.Count()).SingleAsync()).Should().Be(0);
        var repository = new ArticleRepository(db);
        (await repository.GetByIdAsync(article.Id)).Should().BeNull();
        var trashed = await repository.GetByIdIncludingDeletedAsync(article.Id);
        trashed.Should().NotBeNull();
        trashed!.Highlights.Single().Note.Should().Be("note");
        trashed.Restore();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var restored = (await repository.GetByIdAsync(article.Id))!;
        restored.Tags.Should().ContainSingle();
        restored.Highlights.Should().ContainSingle();
        restored.ReadingStatus.Should().Be(ReadingStatus.Read);
        restored.IsFavorite.Should().BeTrue();
        restored.IsArchived.Should().BeTrue();
        restored.ProgressTextOffset.Should().Be(10);
        (await db.Tags.Where(t => t.Id == tag.Id).Select(t => t.Articles.Count()).SingleAsync()).Should().Be(1);
        (await db.Highlights.CountAsync(h => h.ArticleId == article.Id)).Should().Be(1);
    }

    [Fact]
    public async Task NormalizedUrlMatch_IncludesTrashAndSelectsOldestDuplicate()
    {
        var path = Guid.NewGuid().ToString();
        var oldest = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        await using var db = fixture.CreateContext();
        // Raw SQL represents pre-existing URL variants from before normalization.
        var newer = await fixture.SeedArticleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Articles\" SET \"NormalizedUrl\" = {path} WHERE \"Id\" IN ({oldest.Id}, {newer.Id})");
        var found = await new ArticleRepository(db).GetByUrlAsync(path);
        found!.Id.Should().Be(oldest.Id);
        found.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchVector_IncludesSummaryWithMixedLanguageConfiguration()
    {
        var article = await fixture.SeedArticleAsync(a => a.UpdateMetadata("Title", "人工智能 summaryuniqueword"));
        await using var db = fixture.CreateContext();
        (await db.Articles.AnyAsync(a => a.Id == article.Id &&
            a.SearchVector.Matches(EF.Functions.PlainToTsQuery("mixed_zh_en", "summaryuniqueword")))).Should().BeTrue();
        (await db.Articles.AnyAsync(a => a.Id == article.Id &&
            a.SearchVector.Matches(EF.Functions.PlainToTsQuery("mixed_zh_en", "人工智能")))).Should().BeTrue();
    }

    [Fact]
    public async Task Seeder_BackfillsTrashWithoutRewritingExistingCanonicalUrls()
    {
        var empty = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        var present = await fixture.SeedArticleAsync();
        await using var db = fixture.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Articles\" SET \"NormalizedUrl\" = '' WHERE \"Id\" = {empty.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Articles\" SET \"NormalizedUrl\" = 'keep-existing' WHERE \"Id\" = {present.Id}");
        await new DbSeeder(db).InitAsync();
        (await db.Articles.IgnoreQueryFilters().SingleAsync(a => a.Id == empty.Id)).NormalizedUrl.Should().Be(empty.NormalizedUrl);
        (await db.Articles.SingleAsync(a => a.Id == present.Id)).NormalizedUrl.Should().Be("keep-existing");
    }

    [Fact]
    public async Task MigrationAndSeeder_LongUnicodeLegacyUrls_BackfillRestartAndMatchWithoutTruncation()
    {
        var name = "unicode_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {name}", admin).ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;
        await using var db = fixture.CreateContext(connectionString);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE EXTENSION IF NOT EXISTS zhparser;
            CREATE TEXT SEARCH CONFIGURATION mixed_zh_en (PARSER = zhparser);
            ALTER TEXT SEARCH CONFIGURATION mixed_zh_en ADD MAPPING FOR n,v,a,i,e,l,w,s WITH simple;
            ALTER TEXT SEARCH CONFIGURATION mixed_zh_en ADD MAPPING FOR x WITH english_stem;
            """);
        await db.GetService<IMigrator>().MigrateAsync("20250719091116_ApiKey");
        const string prefix = "https://example.com/";
        var unicode = string.Concat(Enumerable.Range(0, 2000 - prefix.Length)
            .Select(i => (char)(0x4e00 + i * 7919 % 20992)));
        const string implicitRootPrefix = "https://example.com?x=";
        var urls = new[]
        {
            prefix + new string('中', 250), prefix + unicode, prefix + unicode[..^1] + "文",
            implicitRootPrefix + new string('a', 2000 - implicitRootPrefix.Length),
            "https://" + new string('a', 1980) + "@example.com"
        };
        var ids = new List<Guid>();
        foreach (var url in urls)
        {
            Uri.IsWellFormedUriString(url, UriKind.Absolute).Should().BeTrue();
            url.Length.Should().BeLessThanOrEqualTo(2000);
            var id = Guid.NewGuid();
            ids.Add(id);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Articles" ("Id", "Url", "Title", "Author", "OriginalContent", "ReadableContent",
                    "Length", "Summary", "ImageUrl", "CreatedAt", "ReadAt", "IsFavorite", "IsArchived")
                VALUES ({id}, {url}, 'Title', '', 'original', 'readable', 8, 'summary', '', {DateTimeOffset.UtcNow},
                    NULL, true, true)
                """);
        }

        await new DbSeeder(db).InitAsync();
        await new DbSeeder(db).InitAsync();
        db.ChangeTracker.Clear();
        var repository = new ArticleRepository(db);
        for (var i = 0; i < urls.Length; i++)
        {
            var canonical = MiniCc.Api.Core.ArticleNs.Domain.Services.UrlNormalizer.Normalize(urls[i]);
            canonical.Should().Be(new Uri(urls[i]).AbsoluteUri);
            (await repository.GetByUrlAsync(canonical))!.Id.Should().Be(ids[i]);
            var escaped = new Uri(urls[i]).AbsoluteUri;
            (await repository.GetByUrlAsync(MiniCc.Api.Core.ArticleNs.Domain.Services.UrlNormalizer.Normalize(escaped)))!.Id.Should().Be(ids[i]);
        }
        (await db.Articles.Select(a => a.NormalizedUrl).Distinct().CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task MigrationAndSeeder_BackfillLegacyStateAndCanonicalUrlsIdempotently()
    {
        var name = "migration_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await new NpgsqlCommand($"CREATE DATABASE {name}", admin).ExecuteNonQueryAsync();
        }
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;
        await using var db = fixture.CreateContext(connectionString);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE EXTENSION IF NOT EXISTS zhparser;
            CREATE TEXT SEARCH CONFIGURATION mixed_zh_en (PARSER = zhparser);
            ALTER TEXT SEARCH CONFIGURATION mixed_zh_en ADD MAPPING FOR n,v,a,i,e,l,w,s WITH simple;
            ALTER TEXT SEARCH CONFIGURATION mixed_zh_en ADD MAPPING FOR x WITH english_stem;
            """);
        await db.GetService<IMigrator>().MigrateAsync("20250719091116_ApiKey");
        var readId = Guid.NewGuid();
        var unreadId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-3);
        var readAt = createdAt.AddDays(1);
        foreach (var id in new[] { readId, unreadId })
        {
            var url = $"https://EXAMPLE.com/{id}/?utm_source=legacy#part";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Articles" ("Id", "Url", "Title", "Author", "OriginalContent", "ReadableContent",
                    "Length", "Summary", "ImageUrl", "CreatedAt", "ReadAt", "IsFavorite", "IsArchived")
                VALUES ({id}, {url}, 'Title', '', 'original', 'readable', 8, 'summary', '', {createdAt},
                    {(id == readId ? (DateTimeOffset?)readAt : null)}, true, true)
                """);
        }
        await db.Database.MigrateAsync();
        var legacy = await db.Articles.OrderBy(a => a.Id).ToListAsync();
        legacy.Single(a => a.Id == readId).ReadingStatus.Should().Be(ReadingStatus.Read);
        legacy.Single(a => a.Id == unreadId).ReadingStatus.Should().Be(ReadingStatus.Unread);
        legacy.Should().OnlyContain(a => a.SavedFromExtensionAt == a.CreatedAt && a.NormalizedUrl == "");
        var seeder = new DbSeeder(db);
        await seeder.InitAsync();
        await seeder.InitAsync();
        db.ChangeTracker.Clear();
        var normalized = await db.Articles.ToListAsync();
        normalized.Should().OnlyContain(a => a.NormalizedUrl == $"https://example.com/{a.Id}");
        normalized.Should().OnlyContain(a => a.IsFavorite && a.IsArchived);
        normalized.Single(a => a.Id == readId).ReadAt.Should().BeCloseTo(readAt, TimeSpan.FromMilliseconds(1));
    }
}
