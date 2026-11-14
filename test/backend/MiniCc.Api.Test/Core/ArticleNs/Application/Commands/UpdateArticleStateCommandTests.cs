using MapsterMapper;
using MiniCc.Api.Core.ArticleNs.Application.Commands;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Commands;

public class UpdateArticleStateCommandTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("reading", false)]
    [InlineData("Read", false)]
    [InlineData("invalid", false)]
    [InlineData("read", true)]
    [InlineData("unread", true)]
    public void Validator_OnlyAcceptsExplicitReadStates(string? state, bool valid)
    {
        var command = new UpdateArticleStateCommand { ArticleId = Guid.NewGuid(), ReadingStatus = state };
        new UpdateArticleStateCommandValidator().Validate(command).IsValid.Should().Be(valid);
    }

    [Fact]
    public void Validator_AcceptsFalseBooleanPatch()
    {
        new UpdateArticleStateCommandValidator().Validate(new UpdateArticleStateCommand
        {
            ArticleId = Guid.NewGuid(), IsFavorite = false
        }).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_ChangesOnlyRequestedStateAndPersists()
    {
        var article = Article.Create(Url.Create("https://example.com"), "Title", "", Content.Create("original", "readable"));
        article.SetFavorite(true);
        article.SetArchived(true);
        article.ApplyProgress(new ReadingPosition(10, 0, .5, 30), "session", DateTimeOffset.UtcNow);
        var articles = new Mock<IArticleRepository>();
        var unit = new Mock<IUnitOfWork>();
        unit.SetupGet(u => u.Articles).Returns(articles.Object);
        articles.Setup(r => r.GetByIdAsync(article.Id, CancellationToken.None)).ReturnsAsync(article);
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<ArticleListItemDto>(article)).Returns(new ArticleListItemDto { Id = article.Id });
        var handler = new UpdateArticleStateCommandHandler(unit.Object, mapper.Object);

        var result = await handler.Handle(new UpdateArticleStateCommand { ArticleId = article.Id, ReadingStatus = "read" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        article.ReadingStatus.Should().Be(ReadingStatus.Read);
        article.ReadAt.Should().NotBeNull();
        article.IsFavorite.Should().BeTrue();
        article.IsArchived.Should().BeTrue();
        article.ProgressTextOffset.Should().Be(10);
        unit.Verify(u => u.SaveChangesAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task Handler_MissingArticleReturnsFailure()
    {
        var unit = new Mock<IUnitOfWork>();
        unit.SetupGet(u => u.Articles).Returns(Mock.Of<IArticleRepository>());
        var result = await new UpdateArticleStateCommandHandler(unit.Object, Mock.Of<IMapper>())
            .Handle(new UpdateArticleStateCommand { ArticleId = Guid.NewGuid(), IsArchived = true }, CancellationToken.None);
        result.Error.Should().Contain("not found");
    }
}
