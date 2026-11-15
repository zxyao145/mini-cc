using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.Services;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Infrastructure;

[Collection("Postgres")]
public class BatchArticlePostgresTests(PostgresFixture fixture)
{
    private BatchArticleCommandHandler Handler(MiniCc.Api.Shared.Data.MiniCcDbContext db)
    {
        var uow = new UnitOfWork(db);
        return new(db, uow, new ArticlePurger(db, uow), NullLogger<BatchArticleCommandHandler>.Instance);
    }

    [Theory]
    [InlineData("markRead")]
    [InlineData("markUnread")]
    [InlineData("favorite")]
    [InlineData("unfavorite")]
    [InlineData("archive")]
    [InlineData("unarchive")]
    public async Task EveryExplicitAction_IsIdempotentAndKeepsReadingPosition(string action)
    {
        var article = await fixture.SeedArticleAsync(a =>
        {
            a.SetFavorite(action == "unfavorite"); a.SetArchived(action == "unarchive");
            a.ApplyProgress(new MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ReadingPosition(15, 2, .5, 30), "p4", DateTimeOffset.UtcNow);
            if (action == "markUnread") a.MarkRead(DateTimeOffset.UtcNow);
        });
        await using var db = fixture.CreateContext(); var handler = Handler(db);
        for (var count = 0; count < 2; count++) (await handler.Handle(new() { Ids = [article.Id], Action = action }, default)).Value.Failed.Should().BeEmpty();
        db.ChangeTracker.Clear(); var saved = await db.Articles.SingleAsync(a => a.Id == article.Id);
        saved.ProgressTextOffset.Should().Be(15);
        if (action is "favorite" or "unfavorite") saved.IsFavorite.Should().Be(action == "favorite");
        if (action is "archive" or "unarchive") saved.IsArchived.Should().Be(action == "archive");
        if (action is "markRead" or "markUnread") saved.ReadingStatus.Should().Be(action == "markRead" ? MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ReadingStatus.Read : MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ReadingStatus.Unread);
    }

    [Fact]
    public async Task BatchTags_AddAndRemoveAreIdempotent()
    {
        var article = await fixture.SeedArticleAsync();
        var tag = MiniCc.Api.Core.TagNs.Domain.AggregatesModel.Tag.Create("P4 " + Guid.NewGuid());
        await using var db = fixture.CreateContext(); db.Tags.Add(tag); await db.SaveChangesAsync(); var handler = Handler(db);
        foreach (var action in new[] { "addTags", "addTags", "removeTags", "removeTags" })
        {
            (await handler.Handle(new() { Ids = [article.Id], Action = action, TagIds = [tag.Id] }, default)).Value.Failed.Should().BeEmpty();
            db.ChangeTracker.Clear();
            (await db.Articles.Include(a => a.Tags).SingleAsync(a => a.Id == article.Id)).Tags.Should().HaveCount(action == "addTags" ? 1 : 0);
        }
        (await db.Tags.AnyAsync(t => t.Id == tag.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveFailure_ClearsTrackerAndContinuesToNextArticle()
    {
        var bad = await fixture.SeedArticleAsync();
        var good = await fixture.SeedArticleAsync();
        await using var db = fixture.CreateContext();
        // The only interpolation is an internally generated Guid in PostgreSQL function DDL.
#pragma warning disable EF1002
        await db.Database.ExecuteSqlRawAsync($"CREATE FUNCTION p4_reject() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Id\" = '{bad.Id}'::uuid THEN RAISE EXCEPTION 'P4 intentional write failure'; END IF; RETURN NEW; END $$");
#pragma warning restore EF1002
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER p4_reject BEFORE UPDATE ON \"Articles\" FOR EACH ROW EXECUTE FUNCTION p4_reject()");
        try
        {
            var result = (await Handler(db).Handle(new() { Ids = [bad.Id, good.Id], Action = "archive" }, default)).Value;
            result.Succeeded.Should().Equal(good.Id);
            result.Failed.Should().ContainSingle(f => f.Id == bad.Id && f.Reason == "error");
            await using var verify = fixture.CreateContext();
            (await verify.Articles.SingleAsync(a => a.Id == bad.Id)).IsArchived.Should().BeFalse();
            (await verify.Articles.SingleAsync(a => a.Id == good.Id)).IsArchived.Should().BeTrue();
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER p4_reject ON \"Articles\"");
            await db.Database.ExecuteSqlRawAsync("DROP FUNCTION p4_reject()");
        }
    }

    [Fact]
    public async Task BatchTrashRestoreAndPurge_PreserveStateNotesProgressAndSharedTags()
    {
        var shared = MiniCc.Api.Core.TagNs.Domain.AggregatesModel.Tag.Create("P4 shared " + Guid.NewGuid());
        var article = await fixture.SeedArticleAsync(a =>
        {
            a.AddTag(shared); a.SetFavorite(true); a.SetArchived(true); a.MarkRead(DateTimeOffset.UtcNow);
            a.ApplyProgress(new MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ReadingPosition(12, 2, .3, 40), "p4", DateTimeOffset.UtcNow);
            a.AddHighlight(MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.Highlight.Create(a.Id, "read", "P4 note", 0, 4));
        });
        var other = await fixture.SeedArticleAsync();
        await using var db = fixture.CreateContext();
        var second = await db.Articles.SingleAsync(a => a.Id == other.Id);
        second.AddTag(await db.Tags.SingleAsync(t => t.Id == shared.Id)); await db.SaveChangesAsync();
        var handler = Handler(db);
        foreach (var action in new[] { "trash", "restore", "trash", "restore" })
            (await handler.Handle(new() { Ids = [article.Id], Action = action }, default)).Value.Failed.Should().BeEmpty();
        db.ChangeTracker.Clear();
        var restored = await db.Articles.Include(a => a.Highlights).Include(a => a.Tags).SingleAsync(a => a.Id == article.Id);
        restored.IsFavorite.Should().BeTrue(); restored.IsArchived.Should().BeTrue(); restored.ReadingStatus.Should().Be(MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ReadingStatus.Read);
        restored.ProgressTextOffset.Should().Be(12); restored.ProgressSessionId.Should().Be("p4"); restored.Highlights.Single().Note.Should().Be("P4 note"); restored.Tags.Single().Id.Should().Be(shared.Id);
        (await handler.Handle(new() { Ids = [article.Id], Action = "trash" }, default)).Value.Failed.Should().BeEmpty();
        (await handler.Handle(new() { Ids = [article.Id], Action = "purge" }, default)).Value.Failed.Should().BeEmpty();
        (await db.Highlights.IgnoreQueryFilters().AnyAsync(h => h.ArticleId == article.Id)).Should().BeFalse();
        (await db.Tags.AnyAsync(t => t.Id == shared.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task MissingTag_RejectsEntireBatch_AndCleanupOnlyPurgesExpiredRows()
    {
        var active = await fixture.SeedArticleAsync();
        var expired = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow.AddDays(-45)));
        var fresh = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        await using var db = fixture.CreateContext();
        (await Handler(db).Handle(new() { Ids = [active.Id], Action = "addTags", TagIds = [Guid.NewGuid()] }, default)).IsFailure.Should().BeTrue();
        (await db.Articles.Include(a => a.Tags).SingleAsync(a => a.Id == active.Id)).Tags.Should().BeEmpty();
        var cleanup = new TrashCleanup(db, new ArticlePurger(db, new UnitOfWork(db)), NullLogger<TrashCleanup>.Instance);
        (await cleanup.PurgeAsync(DateTimeOffset.UtcNow.AddDays(-30), default)).Should().BeGreaterThanOrEqualTo(1);
        (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == expired.Id)).Should().BeFalse();
        (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == fresh.Id)).Should().BeTrue();
        (await db.Articles.AnyAsync(a => a.Id == active.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Batch_IsExplicit_Deduplicates_AndReportsTrashAndMissing()
    {
        var one = await fixture.SeedArticleAsync(a => a.SetFavorite(true));
        var two = await fixture.SeedArticleAsync();
        var trash = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        var missing = Guid.NewGuid();
        await using var db = fixture.CreateContext();
        var uow = new UnitOfWork(db);
        var handler = new BatchArticleCommandHandler(db, uow, new ArticlePurger(db, uow), NullLogger<BatchArticleCommandHandler>.Instance);
        var result = (await handler.Handle(new BatchArticleCommand { Ids = [one.Id, two.Id, one.Id, trash.Id, missing], Action = "favorite" }, default)).Value;
        result.Succeeded.Should().BeEquivalentTo([one.Id, two.Id]);
        result.Failed.Should().Contain(f => f.Id == trash.Id && f.Reason == "inTrash");
        result.Failed.Should().Contain(f => f.Id == missing && f.Reason == "notFound");
        (await db.Articles.Where(a => a.Id == one.Id || a.Id == two.Id).ToListAsync()).Should().OnlyContain(a => a.IsFavorite);
    }

    [Fact]
    public async Task Purge_RechecksCurrentStateAfterWaitingForConcurrentRestore()
    {
        var article = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow.AddDays(-40)));
        await using var restore = fixture.CreateContext();
        await using var tx = await restore.Database.BeginTransactionAsync();
        await restore.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Articles\" SET \"DeletedAt\" = NULL WHERE \"Id\" = {article.Id}");
        await using var purgeDb = fixture.CreateContext();
        // A previously tracked stale snapshot must not win over the restored database row.
        await purgeDb.Articles.IgnoreQueryFilters().SingleAsync(a => a.Id == article.Id);
        var purging = new ArticlePurger(purgeDb, new UnitOfWork(purgeDb)).PurgeAsync(article.Id, DateTimeOffset.UtcNow.AddDays(-30), default);
        await using var monitor = fixture.CreateContext();
        var waiting = 0;
        try
        {
            for (var attempt = 0; attempt < 100 && waiting == 0; attempt++)
            {
                waiting = await monitor.Database.SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%Articles%'").SingleAsync();
                if (waiting == 0) await Task.Delay(20);
            }
            waiting.Should().BeGreaterThan(0);
        }
        finally { await tx.CommitAsync(); }
        (await purging).Should().Be("notInTrash");
        (await monitor.Articles.AnyAsync(a => a.Id == article.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Purge_OnlyExpiredTrash_AndKeepsSharedTags()
    {
        var expired = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow.AddDays(-40)));
        var fresh = await fixture.SeedArticleAsync(a => a.MoveToTrash(DateTimeOffset.UtcNow));
        var active = await fixture.SeedArticleAsync();
        await using var db = fixture.CreateContext();
        var purger = new ArticlePurger(db, new UnitOfWork(db));
        (await purger.PurgeAsync(active.Id, null, default)).Should().Be("notInTrash");
        (await purger.PurgeAsync(fresh.Id, DateTimeOffset.UtcNow.AddDays(-30), default)).Should().Be("notExpired");
        (await purger.PurgeAsync(expired.Id, DateTimeOffset.UtcNow.AddDays(-30), default)).Should().BeNull();
        (await db.Articles.IgnoreQueryFilters().AnyAsync(a => a.Id == expired.Id)).Should().BeFalse();
    }

    [Fact]
    public void Validation_RejectsOversizeUnknownActionsAndMissingTags()
    {
        var validator = new BatchArticleCommandValidator();
        validator.Validate(new BatchArticleCommand() { Ids = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToArray(), Action = "trash" }).IsValid.Should().BeFalse();
        validator.Validate(new BatchArticleCommand() { Ids = [Guid.NewGuid()], Action = "toggle" }).IsValid.Should().BeFalse();
        validator.Validate(new BatchArticleCommand() { Ids = [Guid.NewGuid()], Action = "addTags" }).IsValid.Should().BeFalse();
    }
}
