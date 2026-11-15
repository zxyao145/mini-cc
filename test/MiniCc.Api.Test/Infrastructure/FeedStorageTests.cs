using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using Npgsql;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class FeedStorageTests(PostgresFixture fixture)
{
    [Fact]
    public async Task ProposedVarcharAndFullBtree_CannotStoreValidUnicodeIdentities()
    {
        await using var db = fixture.CreateContext();
        await db.Database.OpenConnectionAsync();
        var original = "https://example.com/" + new string('界', 400);
        var normalized = UrlNormalizer.Normalize(original);
        original.Length.Should().BeLessThan(2000); normalized.Length.Should().BeGreaterThan(2000);
        await db.Database.ExecuteSqlRawAsync("CREATE TEMP TABLE feed_storage_probe (url varchar(2000), key text UNIQUE)");
        var tooLong = async () => await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO feed_storage_probe(url) VALUES ({normalized})");
        (await tooLong.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("22001");
        var key = string.Concat(Enumerable.Range(0, 1800).Select(i => (char)(0x4e00 + i)));
        var tooWide = async () => await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO feed_storage_probe(key) VALUES ({key})");
        (await tooWide.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("54000");
    }
}
