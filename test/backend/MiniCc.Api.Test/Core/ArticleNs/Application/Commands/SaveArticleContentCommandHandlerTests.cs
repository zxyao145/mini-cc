using MapsterMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Commands;

public class SaveArticleContentCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IArticleRepository> _articles = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly ArticleDomainService _domainService = new(new ImageProxyService(Options.Create(new ImageProxyOptions
    {
        SignatureKey = "test-key"
    })));

    public SaveArticleContentCommandHandlerTests()
    {
        _unitOfWork.SetupGet(u => u.Articles).Returns(_articles.Object);
        _articles.Setup(r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Article article, CancellationToken _) => article);
        _mapper.Setup(m => m.Map<ArticleDto>(It.IsAny<Article>()))
            .Returns((object source) => new ArticleDto { Id = ((Article)source).Id });
    }

    [Fact]
    public async Task Handle_PersistsSubmittedHtmlWithoutFetchingTheUrl()
    {
        // This host is deliberately unreachable; saving uses the submitted content.
        var command = new SaveArticleContentCommand("https://unreachable.invalid/article", "<html>Original</html>",
            "<article><p>Extracted正文</p></article>", "Extension title", "Author", "Summary", "");
        Article? persisted = null;
        _articles.Setup(r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()))
            .Callback<Article, CancellationToken>((article, _) => persisted = article)
            .ReturnsAsync((Article article, CancellationToken _) => article);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.OriginalContent.Should().Be(command.OriginalContent);
        persisted.ReadableContent.Should().Be(command.ReadableContent);
        persisted.Title.Should().Be(command.Title);
        persisted.Length.Should().Be("Extracted正文".Length);
        _unitOfWork.Verify(u => u.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateFullUrlAndNewBody_ReplacesBodyAndTitleAndResetsProgress()
    {
        var command = new SaveArticleContentCommand("https://example.com/article", "new original", "new readable",
            "New title", "", "", "");
        var existing = _domainService.CreateArticleFromContent(command.Url, "old original", "old readable", "Old title", "", "", "");
        var revision = existing.ContentRevision;
        var future = DateTimeOffset.UtcNow.AddMinutes(1);
        var progressVersion = future.AddTicks(-future.Ticks % 10);
        var firstSaved = progressVersion.AddDays(-1);
        existing.MarkSavedFromExtension(firstSaved);
        existing.SetFavorite(true);
        existing.SetArchived(true);
        existing.MarkRead(progressVersion);
        existing.ApplyProgress(new ReadingPosition(42, 3, .5, 70), "session", progressVersion);
        var tag = Tag.Create("Keep tag");
        existing.AddTag(tag);
        existing.AddHighlight(Highlight.Create(existing.Id, "old", "keep note", 0, 3));
        var orphan = Highlight.Create(existing.Id, "readable", "orphaned note", 4, 12);
        orphan.AnchorStatus = AnchorStatus.Orphaned;
        existing.AddHighlight(orphan);
        _articles.Setup(r => r.GetByUrlAsync(command.Url, CancellationToken.None)).ReturnsAsync(existing);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        existing.OriginalContent.Should().Be(command.OriginalContent);
        existing.ReadableContent.Should().Be(command.ReadableContent);
        existing.Title.Should().Be(command.Title);
        existing.ContentRevision.Should().Be(revision + 1);
        existing.ProgressUpdatedAt.Should().Be(progressVersion.AddTicks(10));
        existing.ProgressTextOffset.Should().BeNull();
        existing.ProgressBlockIndex.Should().BeNull();
        existing.ProgressBlockRatio.Should().BeNull();
        existing.ProgressPercent.Should().BeNull();
        existing.ProgressSessionId.Should().BeNull();
        existing.Highlights.Should().OnlyContain(h => h.AnchorStatus == AnchorStatus.Pending);
        existing.Highlights.Select(h => h.Note).Should().BeEquivalentTo("keep note", "orphaned note");
        existing.Tags.Should().ContainSingle().Which.Should().BeSameAs(tag);
        existing.IsFavorite.Should().BeTrue();
        existing.IsArchived.Should().BeTrue();
        existing.ReadingStatus.Should().Be(ReadingStatus.Read);
        existing.SavedFromExtensionAt.Should().Be(firstSaved);
        _articles.Verify(r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateFullUrlAndSameRewrittenBody_KeepsProgressHighlightsAndRevision()
    {
        const string url = "https://example.com/article";
        var existing = _domainService.CreateArticleFromContent(url, "old original",
            "<p>readable</p><img src='https://example.com/image.jpg'>", "Old title", "", "", "");
        var savedBody = existing.ReadableContent;
        var revision = existing.ContentRevision;
        var progressVersion = DateTimeOffset.UtcNow;
        existing.ApplyProgress(new ReadingPosition(4, 0, .5, 50), "session", progressVersion);
        existing.AddHighlight(Highlight.Create(existing.Id, "readable", "keep note", 0, 8));
        _articles.Setup(r => r.GetByUrlAsync(url, CancellationToken.None)).ReturnsAsync(existing);
        var command = new SaveArticleContentCommand(url, "new original",
            "<p>readable</p><img src='https://example.com/image.jpg' crossorigin>", "New title", "", "", "");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.Title.Should().Be(command.Title);
        existing.OriginalContent.Should().Be("old original");
        existing.ReadableContent.Should().Be(savedBody);
        existing.ContentRevision.Should().Be(revision);
        existing.ProgressUpdatedAt.Should().Be(progressVersion);
        existing.ProgressTextOffset.Should().Be(4);
        existing.Highlights.Single().AnchorStatus.Should().Be(AnchorStatus.Ok);
    }

    [Fact]
    public async Task Handle_WithDuplicateFullUrl_PreservesBlankOptionalFieldsAndRefreshesNonEmptyOnes()
    {
        const string url = "https://example.com/article";
        var existing = _domainService.CreateArticleFromContent(url, "original", "readable", "Old", "Old author", "Old summary", "https://example.com/old.jpg");
        _articles.Setup(r => r.GetByUrlAsync(url, CancellationToken.None)).ReturnsAsync(existing);
        var command = new SaveArticleContentCommand(url, "original", "readable", " New ", "  ", "\t", "https://example.com/new.jpg");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        existing.Title.Should().Be("New");
        existing.Author.Should().Be("Old author");
        existing.Summary.Should().Be("Old summary");
        existing.ImageUrl.Should().StartWith("/imageProxy/").And.EndWith("/https://example.com/new.jpg");
    }

    [Fact]
    public async Task Handle_WithExistingLink_UpgradesToFullAndFillsEmptyMetadata()
    {
        const string url = "https://example.com/link";
        var existing = _domainService.CreateLink(url, "Link title", "", "", "");
        _articles.Setup(r => r.GetByUrlAsync(url, CancellationToken.None)).ReturnsAsync(existing);
        var command = new SaveArticleContentCommand(url, "new original", "new readable", "New title", "Jane", "New summary", "");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        existing.ContentKind.Should().Be(ContentKind.Full);
        existing.ReadableContent.Should().Be(command.ReadableContent);
        existing.Title.Should().Be("Link title");
        existing.Author.Should().Be("Jane");
        existing.Summary.Should().Be("New summary");
        _articles.Verify(r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyReadableContent_DoesNotPersistArticle()
    {
        var command = new SaveArticleContentCommand("https://example.com/article", "original", " ", "Title", "", "", "");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        _articles.Verify(r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithCanonicalUrlInTrash_RestoresOriginalWithNotesAndState()
    {
        var command = new SaveArticleContentCommand("https://EXAMPLE.com/article/?utm_source=test#top", "new original", "old readable",
            "New title", "", "", "");
        var existing = _domainService.CreateArticleFromContent("https://example.com/article", "old original", "old readable", "Old title", "", "", "");
        existing.SetFavorite(true);
        existing.SetArchived(true);
        existing.MarkRead(DateTimeOffset.UtcNow);
        existing.ApplyProgress(new ReadingPosition(42, 3, .5, 70), "session", DateTimeOffset.UtcNow);
        existing.AddHighlight(Highlight.Create(existing.Id, "old", "note", 0, 3));
        existing.MoveToTrash(DateTimeOffset.UtcNow);
        _articles.Setup(r => r.GetByUrlAsync("https://example.com/article", CancellationToken.None)).ReturnsAsync(existing);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        existing.DeletedAt.Should().BeNull();
        existing.IsFavorite.Should().BeTrue();
        existing.IsArchived.Should().BeTrue();
        existing.ReadingStatus.Should().Be(ReadingStatus.Read);
        existing.ProgressTextOffset.Should().Be(42);
        existing.Highlights.Single().Note.Should().Be("note");
        existing.ReadableContent.Should().Be("old readable");
        existing.SavedFromExtensionAt.Should().NotBeNull();
        _articles.Verify(r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    private SaveArticleContentCommandHandler CreateHandler() => new(
        _unitOfWork.Object, _domainService, _mapper.Object, NullLogger<SaveArticleContentCommandHandler>.Instance);
}
