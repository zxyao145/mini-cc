using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Queries;

public class GetArticleByIdQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<GetArticleByIdQueryHandler>> _loggerMock;
    private readonly GetArticleByIdQueryHandler _handler;

    public GetArticleByIdQueryHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<GetArticleByIdQueryHandler>>();

        _handler = new GetArticleByIdQueryHandler(
            _unitOfWorkMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingArticle_ShouldReturnSuccessResult()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var query = new GetArticleByIdQuery(articleId);
        var cancellationToken = CancellationToken.None;

        var article = CreateTestArticle();
        var expectedDto = CreateTestArticleDto();

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByIdAsync(articleId, cancellationToken))
            .ReturnsAsync(article);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        _mapperMock
            .Setup(m => m.Map<ArticleDto>(article))
            .Returns(expectedDto);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedDto);

        mockArticleRepository.Verify(
            r => r.GetByIdAsync(articleId, cancellationToken),
            Times.Once);

        _mapperMock.Verify(
            m => m.Map<ArticleDto>(article),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentArticle_ShouldReturnFailureResult()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var query = new GetArticleByIdQuery(articleId);
        var cancellationToken = CancellationToken.None;

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByIdAsync(articleId, cancellationToken))
            .ReturnsAsync((Article?)null);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Article not found");

        mockArticleRepository.Verify(
            r => r.GetByIdAsync(articleId, cancellationToken),
            Times.Once);

        _mapperMock.Verify(
            m => m.Map<ArticleDto>(It.IsAny<Article>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var query = new GetArticleByIdQuery(articleId);
        var cancellationToken = CancellationToken.None;
        var exceptionMessage = "Database connection error";
        var exception = new InvalidOperationException(exceptionMessage);

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByIdAsync(articleId, cancellationToken))
            .ThrowsAsync(exception);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to get article");
        result.Error.Should().Contain(exceptionMessage);

        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error getting article {articleId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenMapperThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var query = new GetArticleByIdQuery(articleId);
        var cancellationToken = CancellationToken.None;
        var exceptionMessage = "Mapping error";
        var exception = new InvalidOperationException(exceptionMessage);

        var article = CreateTestArticle();

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByIdAsync(articleId, cancellationToken))
            .ReturnsAsync(article);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        _mapperMock
            .Setup(m => m.Map<ArticleDto>(article))
            .Throws(exception);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to get article");
        result.Error.Should().Contain(exceptionMessage);

        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error getting article {articleId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ShouldCallRepositoryWithCorrectParameters()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var query = new GetArticleByIdQuery(articleId);
        var cancellationToken = new CancellationToken();

        var article = CreateTestArticle();
        var articleDto = CreateTestArticleDto();

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByIdAsync(articleId, cancellationToken))
            .ReturnsAsync(article);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        _mapperMock
            .Setup(m => m.Map<ArticleDto>(article))
            .Returns(articleDto);

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        mockArticleRepository.Verify(
            r => r.GetByIdAsync(articleId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyGuid_ShouldStillCallRepository()
    {
        // Arrange
        var articleId = Guid.Empty;
        var query = new GetArticleByIdQuery(articleId);
        var cancellationToken = CancellationToken.None;

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByIdAsync(articleId, cancellationToken))
            .ReturnsAsync((Article?)null);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        // Act
        var result = await _handler.Handle(query, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Article not found");

        mockArticleRepository.Verify(
            r => r.GetByIdAsync(articleId, cancellationToken),
            Times.Once);
    }

    private static Article CreateTestArticle()
    {
        var url = Url.Create("https://example.com/test");
        var content = Content.Create("&lt;html&gt;Test content&lt;/html&gt;", "Test content");
        return Article.Create(url, "Test Article", "Test Author", content);
    }

    private static ArticleDto CreateTestArticleDto()
    {
        return new ArticleDto
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com/test",
            Title = "Test Article",
            Author = "Test Author",
            ReadableContent = "Test content",
            TextContentLength = 12,
            Summary = "",
            ImageUrl = "",
            CreatedAt = DateTimeOffset.UtcNow,
            ReadAt = null,
            IsArchived = false,
            IsFavorite = false,
            Tags = new List<TagDto>(),
            Highlights = new List<HighlightDto>()
        };
    }
}