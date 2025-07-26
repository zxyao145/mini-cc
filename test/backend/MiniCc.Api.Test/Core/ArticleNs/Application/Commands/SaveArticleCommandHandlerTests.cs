using MapsterMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Commands;

public class SaveArticleCommandHandlerTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IArticleDomainService> _articleDomainServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<ILogger<SaveArticleCommandHandler>> _loggerMock;
    private readonly SaveArticleCommandHandler _handler;

    public SaveArticleCommandHandlerTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _articleDomainServiceMock = new Mock<IArticleDomainService>();
        _mapperMock = new Mock<IMapper>();
        _loggerMock = new Mock<ILogger<SaveArticleCommandHandler>>();

        _handler = new SaveArticleCommandHandler(
            _unitOfWorkMock.Object,
            _articleDomainServiceMock.Object,
            _mapperMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithExistingArticle_ShouldReturnExistingArticleDto()
    {
        // Arrange
        var url = "https://example.com/article";
        var command = new SaveArticleCommand(url);
        var cancellationToken = CancellationToken.None;

        var existingArticle = CreateTestArticle();
        var expectedDto = CreateTestArticleDto();

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByUrlAsync(url, cancellationToken))
            .ReturnsAsync(existingArticle);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        _mapperMock
            .Setup(m => m.Map<ArticleDto>(existingArticle))
            .Returns(expectedDto);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedDto);

        _articleDomainServiceMock.Verify(
            s => s.CreateArticleFromUrlAsync(It.IsAny<string>()), 
            Times.Never);
        
        mockArticleRepository.Verify(
            r => r.AddAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), 
            Times.Never);
        
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), 
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNewArticle_ShouldCreateAndReturnNewArticleDto()
    {
        // Arrange
        var url = "https://example.com/new-article";
        var command = new SaveArticleCommand(url);
        var cancellationToken = CancellationToken.None;

        var newArticle = CreateTestArticle();
        var expectedDto = CreateTestArticleDto();

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByUrlAsync(url, cancellationToken))
            .ReturnsAsync((Article?)null);

        mockArticleRepository
            .Setup(r => r.AddAsync(newArticle, cancellationToken))
            .ReturnsAsync(newArticle);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        _articleDomainServiceMock
            .Setup(s => s.CreateArticleFromUrlAsync(url))
            .ReturnsAsync(newArticle);

        _mapperMock
            .Setup(m => m.Map<ArticleDto>(newArticle))
            .Returns(expectedDto);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedDto);

        _articleDomainServiceMock.Verify(
            s => s.CreateArticleFromUrlAsync(url), 
            Times.Once);
        
        mockArticleRepository.Verify(
            r => r.AddAsync(newArticle, cancellationToken), 
            Times.Once);
        
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(cancellationToken), 
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDomainServiceThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var url = "https://example.com/error-article";
        var command = new SaveArticleCommand(url);
        var cancellationToken = CancellationToken.None;
        var exceptionMessage = "Failed to extract content";

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByUrlAsync(url, cancellationToken))
            .ReturnsAsync((Article?)null);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        _articleDomainServiceMock
            .Setup(s => s.CreateArticleFromUrlAsync(url))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to save article");
        result.Error.Should().Contain(exceptionMessage);

        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error saving article from URL")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var url = "https://example.com/repository-error";
        var command = new SaveArticleCommand(url);
        var cancellationToken = CancellationToken.None;
        var exceptionMessage = "Database connection error";

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByUrlAsync(url, cancellationToken))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to save article");
        result.Error.Should().Contain(exceptionMessage);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesThrowsException_ShouldReturnFailureResult()
    {
        // Arrange
        var url = "https://example.com/save-error";
        var command = new SaveArticleCommand(url);
        var cancellationToken = CancellationToken.None;
        var exceptionMessage = "Save changes failed";

        var newArticle = CreateTestArticle();

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByUrlAsync(url, cancellationToken))
            .ReturnsAsync((Article?)null);

        mockArticleRepository
            .Setup(r => r.AddAsync(newArticle, cancellationToken))
            .ReturnsAsync(newArticle);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException(exceptionMessage));

        _articleDomainServiceMock
            .Setup(s => s.CreateArticleFromUrlAsync(url))
            .ReturnsAsync(newArticle);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to save article");
        result.Error.Should().Contain(exceptionMessage);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldLogErrorOnException()
    {
        // Arrange
        var url = "https://example.com/logging-test";
        var command = new SaveArticleCommand(url);
        var cancellationToken = CancellationToken.None;
        var exception = new InvalidOperationException("Test exception");

        var mockArticleRepository = new Mock<IArticleRepository>();
        mockArticleRepository
            .Setup(r => r.GetByUrlAsync(url, cancellationToken))
            .ThrowsAsync(exception);

        _unitOfWorkMock
            .Setup(u => u.Articles)
            .Returns(mockArticleRepository.Object);

        // Act
        var result = await _handler.Handle(command, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();

        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error saving article from URL: {url}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
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