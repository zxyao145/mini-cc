using FluentAssertions;
using Moq;

namespace ContentHandler.Test;

public class ContentHandlerBaseTests
{
    private class TestContentHandler : ContentHandlerBase
    {
        public override int Order { get; } = 10;

        public override bool ShouldHandle(string url)
        {
            return url.Contains("test.com");
        }

        public override Task<string?> ResolveAsync(string url)
        {
            if (url.Contains("redirect"))
                return Task.FromResult<string?>("https://redirected.com");
            return Task.FromResult<string?>(url);
        }

        public override Task<ContentHandleResult> HandleAsync(string url)
        {
            return Task.FromResult(new ContentHandleResult
            {
                Url = url,
                Title = "Test Title",
                Content = "Test Content",
                Summary = "Test Summary"
            });
        }
    }

    private class DefaultTestContentHandler : ContentHandlerBase
    {
        // Uses default implementations
    }

    [Fact]
    public void Order_ShouldHaveDefaultValue()
    {
        // Arrange & Act
        var handler = new DefaultTestContentHandler();

        // Assert
        handler.Order.Should().Be(0);
    }

    [Fact]
    public void Order_ShouldReturnCustomValue()
    {
        // Arrange & Act
        var handler = new TestContentHandler();

        // Assert
        handler.Order.Should().Be(10);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnOriginalUrl_WhenDefault()
    {
        // Arrange
        var handler = new DefaultTestContentHandler();
        var url = "https://example.com";

        // Act
        var result = await handler.ResolveAsync(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnResolvedUrl_WhenOverridden()
    {
        // Arrange
        var handler = new TestContentHandler();
        var url = "https://redirect.test.com";

        // Act
        var result = await handler.ResolveAsync(url);

        // Assert
        result.Should().Be("https://redirected.com");
    }

    [Fact]
    public void ShouldHandle_ShouldReturnFalse_WhenDefault()
    {
        // Arrange
        var handler = new DefaultTestContentHandler();
        var url = "https://example.com";

        // Act
        var result = handler.ShouldHandle(url);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldHandle_ShouldReturnTrue_WhenUrlMatches()
    {
        // Arrange
        var handler = new TestContentHandler();
        var url = "https://test.com/page";

        // Act
        var result = handler.ShouldHandle(url);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldHandle_ShouldReturnFalse_WhenUrlDoesNotMatch()
    {
        // Arrange
        var handler = new TestContentHandler();
        var url = "https://example.com/page";

        // Act
        var result = handler.ShouldHandle(url);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnBasicResult_WhenDefault()
    {
        // Arrange
        var handler = new DefaultTestContentHandler();
        var url = "https://example.com";

        // Act
        var result = await handler.HandleAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().Be(url);
        result.Title.Should().BeNull();
        result.Content.Should().BeNull();
        result.Summary.Should().Be("");
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCustomResult_WhenOverridden()
    {
        // Arrange
        var handler = new TestContentHandler();
        var url = "https://test.com/page";

        // Act
        var result = await handler.HandleAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().Be(url);
        result.Title.Should().Be("Test Title");
        result.Content.Should().Be("Test Content");
        result.Summary.Should().Be("Test Summary");
    }
}