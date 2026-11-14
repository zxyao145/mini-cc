using MapsterMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
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
    public async Task Handle_WithDuplicateUrl_ReturnsExistingArticleWithoutOverwritingIt()
    {
        var command = new SaveArticleContentCommand("https://example.com/article", "new original", "new readable",
            "New title", "", "", "");
        var existing = _domainService.CreateArticleFromContent(command.Url, "old original", "old readable", "Old title", "", "", "");
        _articles.Setup(r => r.GetByUrlAsync(command.Url, CancellationToken.None)).ReturnsAsync(existing);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        existing.ReadableContent.Should().Be("old readable");
        _articles.Verify(r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        var command = new SaveArticleContentCommand("https://EXAMPLE.com/article/?utm_source=test#top", "new original", "new readable",
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
