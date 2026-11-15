using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Domain.Services;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Commands;

public class DeleteArticleCommandHandlerTests
{
    [Fact]
    public async Task Handle_MovesArticleToTrashWithoutDeletingRelationships()
    {
        var article = Article.Create(Url.Create("https://example.com"), "Title", "", Content.Create("original", "readable"));
        var articles = new Mock<IArticleRepository>();
        var unit = new Mock<IUnitOfWork>();
        unit.SetupGet(u => u.Articles).Returns(articles.Object);
        articles.Setup(r => r.GetByIdAsync(article.Id, CancellationToken.None)).ReturnsAsync(article);
        var handler = new DeleteArticleCommandHandler(unit.Object, Mock.Of<IArticleDomainService>(),
            NullLogger<DeleteArticleCommandHandler>.Instance, Mock.Of<IMediator>());

        var result = await handler.Handle(new DeleteArticleCommand(article.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        article.DeletedAt.Should().NotBeNull();
        articles.Verify(r => r.DeleteAsync(It.IsAny<Article>(), It.IsAny<CancellationToken>()), Times.Never);
        unit.Verify(u => u.SaveChangesAsync(CancellationToken.None), Times.Once);
    }
}
