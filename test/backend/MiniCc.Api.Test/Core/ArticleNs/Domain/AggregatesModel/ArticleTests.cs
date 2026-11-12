using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel;
using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.ArticleNs.Domain.Events;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;
using MiniCc.Api.Core.TagNs.Domain.Events;

namespace MiniCc.Api.Test.Core.ArticleNs.Domain.AggregatesModel;

public class ArticleTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnArticleEntity()
    {
        // Arrange
        var url = Url.Create("https://example.com/article");
        var title = "Test Article";
        var author = "Test Author";
        var content = Content.Create("&lt;html&gt;Test content&lt;/html&gt;", "Test content");
        var summary = "Test summary";
        var imageUrl = "https://example.com/image.jpg";

        // Act
        var article = Article.Create(url, title, author, content, summary, imageUrl);

        // Assert
        article.Url.Should().Be(url);
        article.Title.Should().Be(title);
        article.Author.Should().Be(author);
        article.OriginalContent.Should().Be(content.OriginalContent);
        article.ReadableContent.Should().Be(content.ReadableContent);
        article.Length.Should().Be(content.Length);
        article.Summary.Should().Be(summary);
        article.ImageUrl.Should().Be(imageUrl);
        article.IsArchived.Should().BeFalse();
        article.IsFavorite.Should().BeFalse();
        article.ReadAt.Should().BeNull();
        article.Id.Should().NotBe(Guid.Empty);
        article.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithMinimalData_ShouldUseDefaults()
    {
        // Arrange
        var url = Url.Create("https://example.com/article");
        var title = "Test Article";
        var author = "Test Author";
        var content = Content.Create("&lt;html&gt;Test&lt;/html&gt;", "Test");

        // Act
        var article = Article.Create(url, title, author, content);

        // Assert
        article.Summary.Should().Be(string.Empty);
        article.ImageUrl.Should().Be(string.Empty);
    }

    [Fact]
    public void Create_WithNullOptionalParameters_ShouldUseEmptyStrings()
    {
        // Arrange
        var url = Url.Create("https://example.com/article");
        var title = "Test Article";
        var content = Content.Create("&lt;html&gt;Test&lt;/html&gt;", "Test");

        // Act
        var article = Article.Create(url, title, null!, content, null!, null!);

        // Assert
        article.Author.Should().Be(string.Empty);
        article.Summary.Should().Be(string.Empty);
        article.ImageUrl.Should().Be(string.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyTitle_ShouldThrowArgumentException(string invalidTitle)
    {
        // Arrange
        var url = Url.Create("https://example.com/article");
        var content = Content.Create("&lt;html&gt;Test&lt;/html&gt;", "Test");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            Article.Create(url, invalidTitle, "author", content));
        exception.Message.Should().Contain("Title cannot be null or empty");
        exception.ParamName.Should().Be("title");
    }

    [Fact]
    public void Create_ShouldAddArticleCreatedEvent()
    {
        // Arrange
        var url = Url.Create("https://example.com/article");
        var title = "Test Article";
        var content = Content.Create("&lt;html&gt;Test&lt;/html&gt;", "Test");

        // Act
        var article = Article.Create(url, title, "author", content);

        // Assert
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<ArticleCreatedEvent>();
        
        var createdEvent = (ArticleCreatedEvent)domainEvent;
        createdEvent.ArticleId.Should().Be(article.Id);
        createdEvent.Url.Should().Be(url.Value);
        createdEvent.Title.Should().Be(title);
    }

    [Fact]
    public void UpdateMetadata_WithValidData_ShouldUpdateTitleAndSummary()
    {
        // Arrange
        var article = CreateTestArticle();
        var newTitle = "Updated Title";
        var newSummary = "Updated summary";

        // Act
        article.UpdateMetadata(newTitle, newSummary);

        // Assert
        article.Title.Should().Be(newTitle);
        article.Summary.Should().Be(newSummary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateMetadata_WithNullOrEmptyTitle_ShouldThrowArgumentException(string invalidTitle)
    {
        // Arrange
        var article = CreateTestArticle();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            article.UpdateMetadata(invalidTitle, "summary"));
        exception.Message.Should().Contain("Title cannot be null or empty");
        exception.ParamName.Should().Be("title");
    }

    [Fact]
    public void UpdateMetadata_WithNullSummary_ShouldUseEmptyString()
    {
        // Arrange
        var article = CreateTestArticle();

        // Act
        article.UpdateMetadata("New Title", null!);

        // Assert
        article.Summary.Should().Be(string.Empty);
    }

    [Fact]
    public void UpdateMetadata_ShouldAddArticleUpdatedEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        article.ClearDomainEvents();
        var newTitle = "Updated Title";

        // Act
        article.UpdateMetadata(newTitle, "summary");

        // Assert
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<ArticleUpdatedEvent>();
        
        var updatedEvent = (ArticleUpdatedEvent)domainEvent;
        updatedEvent.ArticleId.Should().Be(article.Id);
        updatedEvent.Title.Should().Be(newTitle);
    }

    [Fact]
    public void StateSetters_AreIdempotentAndIndependent()
    {
        var article = CreateTestArticle();
        var now = DateTimeOffset.UtcNow;
        article.SetFavorite(true);
        article.SetFavorite(true);
        article.SetArchived(true);
        article.SetArchived(true);
        article.ReadingStatus.Should().Be(ReadingStatus.Unread);
        article.MarkRead(now);
        article.MarkRead(now.AddHours(1));
        article.ReadAt.Should().Be(now);
        article.IsFavorite.Should().BeTrue();
        article.IsArchived.Should().BeTrue();
        article.SetFavorite(false);
        article.SetArchived(false);
        article.ReadingStatus.Should().Be(ReadingStatus.Read);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Progress_OnlyPromotesUnreadAndRetainsCompletedStatus(bool completed)
    {
        var article = CreateTestArticle();
        var now = DateTimeOffset.UtcNow;
        if (completed) article.MarkRead(now);
        article.ApplyProgress(new ReadingPosition(23, 2, .4, 61), "session", now);
        article.ReadingStatus.Should().Be(completed ? ReadingStatus.Read : ReadingStatus.Reading);
        article.ProgressTextOffset.Should().Be(23);
        article.ProgressBlockIndex.Should().Be(2);
        article.ProgressBlockRatio.Should().Be(.4);
        article.ProgressPercent.Should().Be(61);
        article.ProgressSessionId.Should().Be("session");
        article.ProgressUpdatedAt.Should().Be(now);
        article.LastReadAt.Should().Be(now);
        article.MarkUnread();
        article.ReadAt.Should().BeNull();
        article.ReadingStatus.Should().Be(ReadingStatus.Unread);
        article.ProgressTextOffset.Should().Be(23);
    }

    [Fact]
    public void ResetProgress_ClearsPositionButPreservesStateAndLastReadAt()
    {
        var article = CreateTestArticle();
        var now = DateTimeOffset.UtcNow;
        article.ApplyProgress(new ReadingPosition(23, 2, .4, 61), "old", now);
        article.MarkRead(now);
        article.ResetProgress(null, now.AddMinutes(1));
        article.ProgressTextOffset.Should().BeNull();
        article.ProgressBlockIndex.Should().BeNull();
        article.ProgressBlockRatio.Should().BeNull();
        article.ProgressPercent.Should().BeNull();
        article.ProgressSessionId.Should().BeNull();
        article.ProgressUpdatedAt.Should().Be(now.AddMinutes(1));
        article.LastReadAt.Should().Be(now);
        article.ReadingStatus.Should().Be(ReadingStatus.Read);
        article.ReadAt.Should().Be(now);
    }

    [Theory]
    [InlineData(ReadingStatus.Unread, false, false)]
    [InlineData(ReadingStatus.Unread, false, true)]
    [InlineData(ReadingStatus.Unread, true, false)]
    [InlineData(ReadingStatus.Unread, true, true)]
    [InlineData(ReadingStatus.Reading, false, false)]
    [InlineData(ReadingStatus.Reading, false, true)]
    [InlineData(ReadingStatus.Reading, true, false)]
    [InlineData(ReadingStatus.Reading, true, true)]
    [InlineData(ReadingStatus.Read, false, false)]
    [InlineData(ReadingStatus.Read, false, true)]
    [InlineData(ReadingStatus.Read, true, false)]
    [InlineData(ReadingStatus.Read, true, true)]
    public void TrashAndRestore_PreserveAllArticleDataAndRepeatedDeleteTime(ReadingStatus status, bool favorite, bool archived)
    {
        var article = CreateTestArticle();
        var now = DateTimeOffset.UtcNow;
        var tag = Tag.Create("tag");
        var highlight = Highlight.Create(article.Id, "text", "note", 0, 4);
        article.AddTag(tag);
        article.AddHighlight(highlight);
        article.ApplyProgress(new ReadingPosition(23, 2, .4, 61), "session", now);
        if (status == ReadingStatus.Read) article.MarkRead(now);
        if (status == ReadingStatus.Unread) article.MarkUnread();
        article.SetFavorite(favorite);
        article.SetArchived(archived);
        var before = System.Text.Json.JsonSerializer.Serialize(article);
        article.MoveToTrash(now);
        article.MoveToTrash(now.AddDays(1));
        article.DeletedAt.Should().Be(now);
        article.Restore();
        System.Text.Json.JsonSerializer.Serialize(article).Should().Be(before);
        article.Restore();
        article.Tags.Should().Contain(tag);
        article.Highlights.Should().Contain(highlight);
    }

    [Fact]
    public void ReplaceContent_ChangesContentAndKindWithoutChangingUserData()
    {
        var article = CreateTestArticle();
        article.SetFavorite(true);
        article.MarkRead(DateTimeOffset.UtcNow);
        var replacement = Content.Create("new original", "new readable", 12);
        article.ReplaceContent(replacement, ContentKind.SummaryOnly);
        article.OriginalContent.Should().Be("new original");
        article.ReadableContent.Should().Be("new readable");
        article.Length.Should().Be(12);
        article.ContentKind.Should().Be(ContentKind.SummaryOnly);
        article.IsFavorite.Should().BeTrue();
        article.ReadingStatus.Should().Be(ReadingStatus.Read);
    }

    [Fact]
    public void AddTag_WithNewTag_ShouldAddTagAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        var tag = Tag.Create("Technology");
        article.ClearDomainEvents();

        // Act
        article.AddTag(tag);

        // Assert
        article.Tags.Should().HaveCount(1);
        article.Tags.Should().Contain(tag);
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<TagAddedToArticleEvent>();
        
        var tagAddedEvent = (TagAddedToArticleEvent)domainEvent;
        tagAddedEvent.ArticleId.Should().Be(article.Id);
        tagAddedEvent.TagId.Should().Be(tag.Id);
        tagAddedEvent.TagName.Should().Be(tag.Name);
    }

    [Fact]
    public void AddTag_WithExistingTagName_ShouldNotAddDuplicateTag()
    {
        // Arrange
        var article = CreateTestArticle();
        var tag1 = Tag.Create("Technology");
        var tag2 = Tag.Create("Technology");
        article.AddTag(tag1);
        article.ClearDomainEvents();

        // Act
        article.AddTag(tag2);

        // Assert
        article.Tags.Should().HaveCount(1);
        article.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTag_WithExistingTag_ShouldRemoveTagAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        var tag = Tag.Create("Technology");
        article.AddTag(tag);
        article.ClearDomainEvents();

        // Act
        article.RemoveTag(tag.Id);

        // Assert
        article.Tags.Should().BeEmpty();
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<TagRemovedFromArticleEvent>();
        
        var tagRemovedEvent = (TagRemovedFromArticleEvent)domainEvent;
        tagRemovedEvent.ArticleId.Should().Be(article.Id);
        tagRemovedEvent.TagId.Should().Be(tag.Id);
        tagRemovedEvent.TagName.Should().Be(tag.Name);
    }

    [Fact]
    public void RemoveTag_WithNonExistentTag_ShouldNotRemoveAnythingOrAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        var nonExistentTagId = Guid.NewGuid();
        article.ClearDomainEvents();

        // Act
        article.RemoveTag(nonExistentTagId);

        // Assert
        article.Tags.Should().BeEmpty();
        article.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddHighlight_WithValidHighlight_ShouldAddHighlightAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        var highlight = Highlight.Create(article.Id, "Selected text", "Note", 0, 13);
        article.ClearDomainEvents();

        // Act
        article.AddHighlight(highlight);

        // Assert
        article.Highlights.Should().HaveCount(1);
        article.Highlights.Should().Contain(highlight);
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<HighlightAddedEvent>();
        
        var highlightAddedEvent = (HighlightAddedEvent)domainEvent;
        highlightAddedEvent.ArticleId.Should().Be(article.Id);
        highlightAddedEvent.HighlightId.Should().Be(highlight.Id);
        highlightAddedEvent.SelectedText.Should().Be(highlight.Text);
    }

    [Fact]
    public void RemoveHighlight_WithExistingHighlight_ShouldRemoveHighlightAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        var highlight = Highlight.Create(article.Id, "Selected text", "Note", 0, 13);
        article.AddHighlight(highlight);
        article.ClearDomainEvents();

        // Act
        article.RemoveHighlight(highlight.Id);

        // Assert
        article.Highlights.Should().BeEmpty();
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<HighlightRemovedEvent>();
        
        var highlightRemovedEvent = (HighlightRemovedEvent)domainEvent;
        highlightRemovedEvent.ArticleId.Should().Be(article.Id);
        highlightRemovedEvent.HighlightId.Should().Be(highlight.Id);
    }

    [Fact]
    public void RemoveHighlight_WithNonExistentHighlight_ShouldNotRemoveAnythingOrAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        var nonExistentHighlightId = Guid.NewGuid();
        article.ClearDomainEvents();

        // Act
        article.RemoveHighlight(nonExistentHighlightId);

        // Assert
        article.Highlights.Should().BeEmpty();
        article.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ParameterlessConstructor_ShouldCreateEmptyArticle()
    {
        // Act
        var article = new Article();

        // Assert
        article.Tags.Should().NotBeNull();
        article.Tags.Should().BeEmpty();
        article.Highlights.Should().NotBeNull();
        article.Highlights.Should().BeEmpty();
    }

    private static Article CreateTestArticle()
    {
        var url = Url.Create("https://example.com/test");
        var content = Content.Create("&lt;html&gt;Test&lt;/html&gt;", "Test");
        return Article.Create(url, "Test Article", "Test Author", content);
    }
}