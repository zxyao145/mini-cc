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

public class SaveLinkCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IArticleRepository> _articles = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly ArticleDomainService _domainService = new(new ImageProxyService(Options.Create(new ImageProxyOptions
    {
        SignatureKey = "test-key"
    })));

    public SaveLinkCommandHandlerTests()
    {
        _unitOfWork.SetupGet(u => u.Articles).Returns(_articles.Object);
        _mapper.Setup(m => m.Map<ArticleDto>(It.IsAny<Article>()))
            .Returns((object source) => new ArticleDto { Id = ((Article)source).Id });
    }

    [Fact]
    public async Task Handle_WithNewUrl_PersistsLinkMarkedAsSavedFromExtension()
    {
        var command = new SaveLinkCommand("https://example.com/video", "Video", null, "Description", null);
        Article? persisted = null;
        _articles.Setup(r => r.AddAsync(It.IsAny<Article>(), CancellationToken.None))
            .Callback<Article, CancellationToken>((article, _) => persisted = article)
            .ReturnsAsync((Article article, CancellationToken _) => article);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.ContentKind.Should().Be(ContentKind.Link);
        persisted.SavedFromExtensionAt.Should().NotBeNull();
        persisted.Title.Should().Be(command.Title);
        persisted.Summary.Should().Be(command.Summary);
        _unitOfWork.Verify(u => u.LockArticleCreationAsync(CancellationToken.None), Times.Once);
        _unitOfWork.Verify(u => u.CommitTransactionAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingFullArticle_DoesNotDowngradeOrOverwrite()
    {
        const string url = "https://example.com/article";
        var existing = _domainService.CreateArticleFromContent(url, "original", "readable", "Original title", "", "", "");
        _articles.Setup(r => r.GetByUrlAsync(url, CancellationToken.None)).ReturnsAsync(existing);

        var result = await CreateHandler().Handle(new(url + "?utm_source=test#top", "New title", null, "New summary", null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        existing.ContentKind.Should().Be(ContentKind.Full);
        existing.ReadableContent.Should().Be("readable");
        existing.Title.Should().Be("Original title");
        existing.SavedFromExtensionAt.Should().NotBeNull();
        _articles.Verify(r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingLink_KeepsExistingMetadata()
    {
        const string url = "https://example.com/video";
        var existing = _domainService.CreateLink(url, "Original title", "", "Original summary", "");
        _articles.Setup(r => r.GetByUrlAsync(url, CancellationToken.None)).ReturnsAsync(existing);

        var result = await CreateHandler().Handle(new(url, "New title", null, "New summary", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        existing.Title.Should().Be("Original title");
        existing.Summary.Should().Be("Original summary");
    }

    [Fact]
    public async Task Handle_WithTrashedArticle_RestoresIt()
    {
        const string url = "https://example.com/video";
        var existing = _domainService.CreateLink(url, "Video", "", "", "");
        existing.MoveToTrash(DateTimeOffset.UtcNow);
        _articles.Setup(r => r.GetByUrlAsync(url, CancellationToken.None)).ReturnsAsync(existing);

        var result = await CreateHandler().Handle(new(url, "Video", null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(existing.Id);
        existing.DeletedAt.Should().BeNull();
    }

    private SaveLinkCommandHandler CreateHandler() => new(
        _unitOfWork.Object, _domainService, _mapper.Object, NullLogger<SaveLinkCommandHandler>.Instance);
}
