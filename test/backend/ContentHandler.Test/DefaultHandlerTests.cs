using FluentAssertions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text;
using ContentHandler.WebSite;

namespace ContentHandler.Test;

public class DefaultHandlerTests
{
    private readonly Mock<ILogger<DefaultHandler>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly DefaultHandler _handler;

    public DefaultHandlerTests()
    {
        _loggerMock = new Mock<ILogger<DefaultHandler>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient("default")).Returns(_httpClient);
        
        _handler = new DefaultHandler(_loggerMock.Object, _httpClientFactoryMock.Object);
    }

    [Fact]
    public void Order_ShouldReturnMaxValue()
    {
        // Act & Assert
        _handler.Order.Should().Be(int.MaxValue);
    }

    [Fact]
    public void ShouldHandle_ShouldAlwaysReturnTrue()
    {
        // Arrange
        var testUrls = new[]
        {
            "https://example.com",
            "http://test.com",
            "https://github.com/user/repo",
            ""
        };

        // Act & Assert
        foreach (var url in testUrls)
        {
            _handler.ShouldHandle(url).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnOriginalUrl()
    {
        // Arrange
        var url = "https://example.com";

        // Act
        var result = await _handler.ResolveAsync(url);

        // Assert
        result.Should().Be(url);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnContentHandleResult_WhenHttpRequestSucceeds()
    {
        // Arrange
        var url = "https://example.com";
        var htmlContent = @"
            <html>
                <head>
                    <title>Test Page Title</title>
                    <meta name='author' content='John Doe' />
                </head>
                <body>
                    <p>This is a test page with some content for testing purposes.</p>
                </body>
            </html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().Be(url);
        result.Title.Should().Be("Test Page Title");
        result.Author.Should().Be("John Doe");
        result.OriginalContent.Should().Be(htmlContent);
        result.Content.Should().Be(htmlContent);
        result.ContentType.Should().Be("text/html");
        result.Dom.Should().NotBeNull();
        result.Summary.Should().NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_ShouldExtractTitleFromHtml()
    {
        // Arrange
        var url = "https://example.com";
        var htmlContent = "<html><head><title>My Awesome Page</title></head><body></body></html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.Title.Should().Be("My Awesome Page");
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyTitle_WhenNoTitleTag()
    {
        // Arrange
        var url = "https://example.com";
        var htmlContent = "<html><head></head><body><h1>No Title Tag</h1></body></html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.Title.Should().Be("");
    }

    [Theory]
    [InlineData("author", "Jane Smith")]
    [InlineData("article:author", "Bob Johnson")]
    [InlineData("dc.creator", "Alice Brown")]
    public async Task HandleAsync_ShouldExtractAuthor_WhenMetaTagExists(string nameValue, string authorName)
    {
        // Arrange
        var url = "https://example.com";
        var htmlContent = nameValue == "article:author" 
            ? $@"
            <html>
                <head>
                    <title>Test</title>
                    <meta property='{nameValue}' content='{authorName}' />
                </head>
                <body></body>
            </html>"
            : $@"
            <html>
                <head>
                    <title>Test</title>
                    <meta name='{nameValue}' content='{authorName}' />
                </head>
                <body></body>
            </html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.Author.Should().Be(authorName);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyAuthor_WhenNoAuthorMeta()
    {
        // Arrange
        var url = "https://example.com";
        var htmlContent = "<html><head><title>Test</title></head><body></body></html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.Author.Should().Be("");
    }

    [Fact]
    public async Task HandleAsync_ShouldTruncateSummary_WhenContentIsLong()
    {
        // Arrange
        var url = "https://example.com";
        var longContent = string.Join(" ", Enumerable.Repeat("word", 100));
        var htmlContent = $"<html><head><title>Test</title></head><body>{longContent}</body></html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.Summary.Should().EndWith("...");
        // The summary is created from the full HTML content, so we check that it was truncated
        var actualWordCount = result.Summary.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        actualWordCount.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnFullContent_WhenContentIsShort()
    {
        // Arrange
        var url = "https://example.com";
        var shortContent = "Short content with less than fifty words here.";
        var htmlContent = $"<html><head><title>Test</title></head><body>{shortContent}</body></html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "text/html")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.Summary.Should().Be(htmlContent);
        result.Summary.Should().NotEndWith("...");
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowAndLog_WhenHttpRequestFails()
    {
        // Arrange
        var url = "https://example.com";
        var exception = new HttpRequestException("Network error");

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _handler.HandleAsync(url));

        // Verify logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to scrape URL")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowAndLog_WhenHttpResponseIsNotSuccessful()
    {
        // Arrange
        var url = "https://example.com";
        var httpResponse = new HttpResponseMessage(HttpStatusCode.NotFound);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _handler.HandleAsync(url));

        // Verify logging
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to scrape URL")),
                It.IsAny<HttpRequestException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldSetContentType_WhenProvided()
    {
        // Arrange
        var url = "https://example.com";
        var htmlContent = "<html><head><title>Test</title></head><body></body></html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8, "application/xhtml+xml")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.ContentType.Should().Be("application/xhtml+xml");
    }

    [Fact]
    public async Task HandleAsync_ShouldSetEmptyContentType_WhenNotProvided()
    {
        // Arrange
        var url = "https://example.com";
        var htmlContent = "<html><head><title>Test</title></head><body></body></html>";

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent, Encoding.UTF8)
        };
        httpResponse.Content.Headers.ContentType = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _handler.HandleAsync(url);

        // Assert
        result.ContentType.Should().Be("");
    }
}