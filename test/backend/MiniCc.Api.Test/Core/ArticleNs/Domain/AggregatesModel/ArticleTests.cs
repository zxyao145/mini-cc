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
    public void MarkAsRead_WhenNotRead_ShouldSetReadAtAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        article.ClearDomainEvents();

        // Act
        article.MarkAsRead();

        // Assert
        article.ReadAt.Should().NotBeNull();
        article.ReadAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<ArticleReadEvent>();
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_ShouldNotChangeReadAtOrAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        article.MarkAsRead();
        var originalReadAt = article.ReadAt;
        article.ClearDomainEvents();

        // Act
        article.MarkAsRead();

        // Assert
        article.ReadAt.Should().Be(originalReadAt);
        article.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ToggleFavorite_WhenNotFavorite_ShouldSetToFavoriteAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        article.ClearDomainEvents();

        // Act
        article.ToggleFavorite();

        // Assert
        article.IsFavorite.Should().BeTrue();
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<ArticleFavoritedEvent>();
        
        var favoritedEvent = (ArticleFavoritedEvent)domainEvent;
        favoritedEvent.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void ToggleFavorite_WhenFavorite_ShouldSetToNotFavoriteAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        article.ToggleFavorite(); // Make it favorite first
        article.ClearDomainEvents();

        // Act
        article.ToggleFavorite();

        // Assert
        article.IsFavorite.Should().BeFalse();
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<ArticleFavoritedEvent>();
        
        var favoritedEvent = (ArticleFavoritedEvent)domainEvent;
        favoritedEvent.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void ToggleArchive_WhenNotArchived_ShouldSetToArchivedAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        article.ClearDomainEvents();

        // Act
        article.ToggleArchive();

        // Assert
        article.IsArchived.Should().BeTrue();
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<ArticleArchivedEvent>();
        
        var archivedEvent = (ArticleArchivedEvent)domainEvent;
        archivedEvent.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void ToggleArchive_WhenArchived_ShouldSetToNotArchivedAndAddEvent()
    {
        // Arrange
        var article = CreateTestArticle();
        article.ToggleArchive(); // Make it archived first
        article.ClearDomainEvents();

        // Act
        article.ToggleArchive();

        // Assert
        article.IsArchived.Should().BeFalse();
        
        article.DomainEvents.Should().HaveCount(1);
        var domainEvent = article.DomainEvents.First();
        domainEvent.Should().BeOfType<ArticleArchivedEvent>();
        
        var archivedEvent = (ArticleArchivedEvent)domainEvent;
        archivedEvent.IsArchived.Should().BeFalse();
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