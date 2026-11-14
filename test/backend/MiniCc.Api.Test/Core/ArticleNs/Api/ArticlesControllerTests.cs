using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MiniCc.Api.Controllers.ArticleNs;
using MiniCc.Api.Controllers.ArticleNs.Requests;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Application.Queries;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Test.Core.ArticleNs.Api;

public class ArticlesControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<ArticlesController>> _loggerMock;
    private readonly ArticlesController _controller;

    public ArticlesControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<ArticlesController>>();
        _controller = new ArticlesController(_mediatorMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetArticles_ReturnsPageAndPassesFilters()
    {
        var request = new ArticleListRequest { View = "favorites", Q = "test" };
        var page = new ArticleListPage([], 126, null);
        _mediatorMock.Setup(m => m.Send(It.Is<GetArticleListQuery>(q => q.Request == request), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ArticleListPage>.Success(page));
        var response = await _controller.GetArticles(request);
        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Fact]
    public async Task GetArticles_InvalidRequestReturnsBadRequest()
    {
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetArticleListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ArticleListPage>.Failure("Invalid view"));
        (await _controller.GetArticles(new())).Result.Should().BeOfType<BadRequestObjectResult>();
    }


    #region GetArticle Tests

    [Fact]
    public async Task GetArticle_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var article = CreateTestArticleDto();
        var result = Result<ArticleDto>.Success(article);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetArticleByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.GetArticle(articleId);

        // Assert
        var okResult = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(article);

        _mediatorMock.Verify(m => m.Send(
            It.Is<GetArticleByIdQuery>(q => q.Id == articleId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetArticle_WithNotFoundError_ShouldReturnNotFound()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var errorMessage = "Article not found";
        var result = Result<ArticleDto>.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetArticleByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.GetArticle(articleId);

        // Assert
        var notFoundResult = response.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be(errorMessage);
    }

    [Fact]
    public async Task GetArticle_WithOtherError_ShouldReturnBadRequest()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var errorMessage = "Database connection error";
        var result = Result<ArticleDto>.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetArticleByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.GetArticle(articleId);

        // Assert
        var badRequestResult = response.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be(errorMessage);

        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error getting article {articleId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SaveArticleContent Tests

    [Fact]
    public async Task SaveArticleContent_WithValidCommand_ShouldReturnCreatedResult()
    {
        // Arrange
        var command = new SaveArticleContentCommand("https://example.com/article", "<html>Original</html>", "<p>Readable</p>", "Title", "", "", "");
        var article = CreateTestArticleDto();
        var result = Result<ArticleDto>.Success(article);

        _mediatorMock
            .Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.SaveArticleContent(command);

        // Assert
        var createdResult = response.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ArticlesController.GetArticle));
        createdResult.RouteValues!["id"].Should().Be(article.Id);
        createdResult.Value.Should().Be(article);

        _mediatorMock.Verify(m => m.Send(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveArticleContent_WithFailureResult_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new SaveArticleContentCommand("https://example.com/article", "<html>Original</html>", "<p>Readable</p>", "Title", "", "", "");
        var errorMessage = "Failed to save article";
        var result = Result<ArticleDto>.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.SaveArticleContent(command);

        // Assert
        var badRequestResult = response.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be(errorMessage);

        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error saving article")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region UpdateArticle Tests

    [Fact]
    public async Task UpdateArticle_WithValidData_ShouldReturnOkResult()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var command = new UpdateArticleCommand { Title = "Updated Title", Summary = "Updated Summary" };
        var updatedArticle = CreateTestArticleDto();
        var result = Result<ArticleDto>.Success(updatedArticle);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpdateArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.UpdateArticle(articleId, command);

        // Assert
        var okResult = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updatedArticle);

        command.Id.Should().Be(articleId);
        _mediatorMock.Verify(m => m.Send(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateArticle_WithNotFoundError_ShouldReturnNotFound()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var command = new UpdateArticleCommand { Title = "Updated Title" };
        var errorMessage = "Article not found";
        var result = Result<ArticleDto>.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<UpdateArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.UpdateArticle(articleId, command);

        // Assert
        var notFoundResult = response.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be(errorMessage);
    }

    #endregion

    [Fact]
    public async Task UpdateState_SendsExplicitValuesAndReturnsArticle()
    {
        var id = Guid.NewGuid();
        var command = new UpdateArticleStateCommand { IsFavorite = false, ReadingStatus = "read" };
        var article = new ArticleListItemDto { Id = Guid.NewGuid() };
        _mediatorMock.Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ArticleListItemDto>.Success(article));

        var response = await _controller.UpdateState(id, command);

        command.ArticleId.Should().Be(id);
        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(article);
    }

    [Fact]
    public async Task UpdateState_MissingArticleReturnsNotFound()
    {
        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateArticleStateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ArticleListItemDto>.Failure("article not found"));
        var response = await _controller.UpdateState(Guid.NewGuid(), new() { IsArchived = true });
        response.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateState_ValidationFailureReturnsBadRequest()
    {
        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateArticleStateCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException("State patch is empty."));
        var response = await _controller.UpdateState(Guid.NewGuid(), new());
        response.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RestoreArticle_ReturnsRestoredArticle()
    {
        var article = new ArticleListItemDto { Id = Guid.NewGuid() };
        _mediatorMock.Setup(m => m.Send(It.Is<RestoreArticleCommand>(c => c.ArticleId == article.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ArticleListItemDto>.Success(article));
        var response = await _controller.RestoreArticle(article.Id);
        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(article);
    }

    #region DeleteArticle Tests

    [Fact]
    public async Task DeleteArticle_WithValidId_ShouldReturnNoContent()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var result = Result.Success();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.DeleteArticle(articleId);

        // Assert
        response.Should().BeOfType<NoContentResult>();

        _mediatorMock.Verify(m => m.Send(
            It.Is<DeleteArticleCommand>(c => c.ArticleId == articleId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteArticle_WithNotFoundError_ShouldReturnNotFound()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var errorMessage = "Article not found";
        var result = Result.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.DeleteArticle(articleId);

        // Assert
        var notFoundResult = response.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be(errorMessage);
    }

    [Fact]
    public async Task DeleteArticle_WithOtherError_ShouldReturnBadRequest()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var errorMessage = "Database error";
        var result = Result.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<DeleteArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.DeleteArticle(articleId);

        // Assert
        var badRequestResult = response.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be(errorMessage);

        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Error deleting article {articleId}")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region AddTag Tests

    [Fact]
    public async Task AddTag_WithValidData_ShouldReturnOkResult()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var request = new AddTagRequest { Name = "Technology", Color = "#FF5733" };
        var tagDto = new TagDto { Id = Guid.NewGuid(), Name = "Technology", Color = "#FF5733" };
        var result = Result<TagDto>.Success(tagDto);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddTagToArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.AddTag(articleId, request);

        // Assert
        var okResult = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(tagDto);

        _mediatorMock.Verify(m => m.Send(
            It.Is<AddTagToArticleCommand>(c => c.ArticleId == articleId && c.Name == request.Name && c.Color == request.Color),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddTag_WithNotFoundError_ShouldReturnNotFound()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var request = new AddTagRequest { Name = "Technology", Color = "#FF5733" };
        var errorMessage = "Article not found";
        var result = Result<TagDto>.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddTagToArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.AddTag(articleId, request);

        // Assert
        var notFoundResult = response.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be(errorMessage);
    }

    #endregion

    #region RemoveTag Tests

    [Fact]
    public async Task RemoveTag_WithValidIds_ShouldReturnNoContent()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var result = Result.Success();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveTagFromArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.RemoveTag(articleId, tagId);

        // Assert
        response.Should().BeOfType<NoContentResult>();

        _mediatorMock.Verify(m => m.Send(
            It.Is<RemoveTagFromArticleCommand>(c => c.ArticleId == articleId && c.TagId == tagId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveTag_WithNotFoundError_ShouldReturnNotFound()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var errorMessage = "Article not found";
        var result = Result.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RemoveTagFromArticleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.RemoveTag(articleId, tagId);

        // Assert
        var notFoundResult = response.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be(errorMessage);
    }

    #endregion

    #region AddHighlight Tests

    [Fact]
    public async Task AddHighlight_WithValidData_ShouldReturnOkResult()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var command = new AddHighlightCommand { Text = "Important text", Note = "Note", StartOffset = 0, EndOffset = 10 };
        var highlightDto = new HighlightDto { Id = Guid.NewGuid(), Text = "Important text", Note = "Note" };
        var result = Result<HighlightDto>.Success(highlightDto);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddHighlightCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.AddHighlight(articleId, command);

        // Assert
        var okResult = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(highlightDto);

        command.ArticleId.Should().Be(articleId);
        _mediatorMock.Verify(m => m.Send(command, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddHighlight_WithNotFoundError_ShouldReturnNotFound()
    {
        // Arrange
        var articleId = Guid.NewGuid();
        var command = new AddHighlightCommand { Text = "Important text", Note = "Note", StartOffset = 0, EndOffset = 10 };
        var errorMessage = "Article not found";
        var result = Result<HighlightDto>.Failure(errorMessage);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<AddHighlightCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var response = await _controller.AddHighlight(articleId, command);

        // Assert
        var notFoundResult = response.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be(errorMessage);
    }

    #endregion

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
            Summary = "Test summary",
            ImageUrl = "https://example.com/image.jpg",
            CreatedAt = DateTimeOffset.UtcNow,
            ReadAt = null,
            IsArchived = false,
            IsFavorite = false,
            Tags = new List<TagDto>(),
            Highlights = new List<HighlightDto>()
        };
    }
}