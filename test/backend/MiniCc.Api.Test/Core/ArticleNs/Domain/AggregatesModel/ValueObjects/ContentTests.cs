using MiniCc.Api.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;

namespace MiniCc.Api.Test.Core.ArticleNs.Domain.AggregatesModel.ValueObjects;

public class ContentTests
{
    [Fact]
    public void Create_WithValidData_ShouldReturnContentValueObject()
    {
        // Arrange
        var originalContent = "&lt;html&gt;&lt;body&gt;Test content&lt;/body&gt;&lt;/html&gt;";
        var readableContent = "Test content";

        // Act
        var content = Content.Create(originalContent, readableContent);

        // Assert
        content.OriginalContent.Should().Be(originalContent);
        content.ReadableContent.Should().Be(readableContent);
        content.Length.Should().Be(readableContent.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WithNullOrEmptyOriginalContent_ShouldThrowArgumentException(string invalidOriginalContent)
    {
        // Arrange
        var readableContent = "Some readable content";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Content.Create(invalidOriginalContent, readableContent));
        exception.Message.Should().Contain("Original content cannot be null or empty");
        exception.ParamName.Should().Be("originalContent");
    }

    [Fact]
    public void Create_WithNullReadableContent_ShouldUseEmptyString()
    {
        // Arrange
        var originalContent = "&lt;html&gt;&lt;body&gt;Test&lt;/body&gt;&lt;/html&gt;";

        // Act
        var content = Content.Create(originalContent, null!);

        // Assert
        content.OriginalContent.Should().Be(originalContent);
        content.ReadableContent.Should().Be(string.Empty);
        content.Length.Should().Be(0);
    }

    [Fact]
    public void Create_WithEmptyReadableContent_ShouldSetLengthToZero()
    {
        // Arrange
        var originalContent = "&lt;html&gt;&lt;body&gt;Test&lt;/body&gt;&lt;/html&gt;";
        var readableContent = "";

        // Act
        var content = Content.Create(originalContent, readableContent);

        // Assert
        content.OriginalContent.Should().Be(originalContent);
        content.ReadableContent.Should().Be(readableContent);
        content.Length.Should().Be(0);
    }

    [Fact]
    public void Create_WithLongReadableContent_ShouldCalculateCorrectLength()
    {
        // Arrange
        var originalContent = "&lt;html&gt;Long content here&lt;/html&gt;";
        var readableContent = "This is a longer piece of readable content for testing length calculation.";

        // Act
        var content = Content.Create(originalContent, readableContent);

        // Assert
        content.OriginalContent.Should().Be(originalContent);
        content.ReadableContent.Should().Be(readableContent);
        content.Length.Should().Be(readableContent.Length);
    }

    [Fact]
    public void Equality_WithSameContent_ShouldBeEqual()
    {
        // Arrange
        var originalContent = "&lt;html&gt;&lt;body&gt;Test&lt;/body&gt;&lt;/html&gt;";
        var readableContent = "Test";
        var content1 = Content.Create(originalContent, readableContent);
        var content2 = Content.Create(originalContent, readableContent);

        // Act & Assert
        content1.Should().Be(content2);
        (content1 == content2).Should().BeTrue();
        content1.GetHashCode().Should().Be(content2.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentOriginalContent_ShouldNotBeEqual()
    {
        // Arrange
        var readableContent = "Test";
        var content1 = Content.Create("&lt;html&gt;Test1&lt;/html&gt;", readableContent);
        var content2 = Content.Create("&lt;html&gt;Test2&lt;/html&gt;", readableContent);

        // Act & Assert
        content1.Should().NotBe(content2);
        (content1 == content2).Should().BeFalse();
    }

    [Fact]
    public void Equality_WithDifferentReadableContent_ShouldNotBeEqual()
    {
        // Arrange
        var originalContent = "&lt;html&gt;&lt;body&gt;Test&lt;/body&gt;&lt;/html&gt;";
        var content1 = Content.Create(originalContent, "Test1");
        var content2 = Content.Create(originalContent, "Test2");

        // Act & Assert
        content1.Should().NotBe(content2);
        (content1 == content2).Should().BeFalse();
    }
}