using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class ReadingProgressPostgresTests(PostgresFixture fixture)
{
    private async Task<MiniCc.Api.Shared.Result<ProgressSaveDto>> Save(Guid id, string session, DateTimeOffset? basis = null, bool reset = false)
    {
        await using var db = fixture.CreateContext();
        return await new UpdateProgressCommandHandler(new UnitOfWork(db)).Handle(new UpdateProgressCommand
        { ArticleId = id, SessionId = session, BaseUpdatedAt = basis, TextOffset = 20, BlockIndex = 2, BlockRatio = .4, Percent = 40, Reset = reset }, CancellationToken.None);
    }

    [Fact]
    public async Task Save_TransitionsUnreadAndAllowsSameSessionButRejectsStaleOtherSession()
    {
        var article = await fixture.SeedArticleAsync();
        var first = (await Save(article.Id, "one")).Value;
        first.ReadingStatus.Should().Be(ReadingStatus.Reading);
        first.LastReadAt.Should().NotBeNull();
        (await Save(article.Id, "one")).Value.Current.Should().BeNull();
        var conflict = (await Save(article.Id, "two")).Value.Current;
        conflict.Should().NotBeNull();
        conflict!.Percent.Should().Be(40);
        (await Save(article.Id, "two", conflict.ProgressUpdatedAt)).Value.Current.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentSessions_WithSameBase_OnlyOneMaySave()
    {
        var article = await fixture.SeedArticleAsync();
        var initial = (await Save(article.Id, "initial")).Value;
        // Force both contenders to reach the database before either can commit. A naive
        // read/check/write implementation reads the same old version and incorrectly accepts both.
        await using var blocker = fixture.CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Articles\" WHERE \"Id\" = {article.Id} FOR UPDATE");
        var contenders = Task.WhenAll(Save(article.Id, "one", initial.ProgressUpdatedAt), Save(article.Id, "two", initial.ProgressUpdatedAt));
        await using var monitor = fixture.CreateContext();
        var waiting = 0;
        try
        {
            for (var attempt = 0; attempt < 100 && waiting < 2; attempt++)
            {
                waiting = await monitor.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%Articles%'").SingleAsync();
                if (waiting < 2) await Task.Delay(20);
            }
            waiting.Should().BeGreaterThanOrEqualTo(2, "both saves must be waiting on the held PostgreSQL row lock");
        }
        finally { await transaction.CommitAsync(); }
        var results = await contenders;
        results.Count(r => r.Value.Current == null).Should().Be(1);
        results.Count(r => r.Value.Current != null).Should().Be(1);
    }

    [Fact]
    public async Task Reset_RetainsReadStateAndVersionsNullPosition_ThenRejectsOldPage()
    {
        var article = await fixture.SeedArticleAsync(a => a.MarkRead(DateTimeOffset.UtcNow));
        var first = (await Save(article.Id, "one")).Value;
        first.ReadingStatus.Should().Be(ReadingStatus.Read);
        var reset = (await Save(article.Id, "two", reset: true)).Value;
        reset.ReadingStatus.Should().Be(ReadingStatus.Read);
        reset.ProgressUpdatedAt.Should().BeAfter(first.ProgressUpdatedAt);
        await using var db = fixture.CreateContext();
        var saved = await db.Articles.FindAsync(article.Id);
        saved!.ProgressTextOffset.Should().BeNull();
        saved.ProgressSessionId.Should().Be("two");
        (await Save(article.Id, "one", first.ProgressUpdatedAt)).Value.Current.Should().NotBeNull();
        (await Save(article.Id, "two", reset.ProgressUpdatedAt)).Value.Current.Should().BeNull();
    }

    [Fact]
    public async Task MissingAndTrashedArticles_ReturnNotFound()
    {
        (await Save(Guid.NewGuid(), "one")).Error.Should().Contain("not found");
        var article = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        (await Save(article.Id, "one")).Error.Should().Contain("not found");
    }
}
