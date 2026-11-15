using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;

namespace MiniCc.Api.Test.Core.TagNs.Domain.AggregatesModel;

public class TagTests
{
    [Fact]
    public void Create_WithValidName_ShouldReturnTagEntity()
    {
        // Arrange
        var tagName = "Technology";

        // Act
        var tag = Tag.Create(tagName);

        // Assert
        tag.Name.Should().Be(tagName);
        tag.Color.Should().Be(TagColor.Default);
        tag.Id.Should().NotBe(Guid.Empty);
        tag.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithValidNameAndColor_ShouldReturnTagEntityWithColor()
    {
        // Arrange
        var tagName = "Important";
        var color = TagColor.Create("#FF5733");

        // Act
        var tag = Tag.Create(tagName, color);

        // Assert
        tag.Name.Should().Be(tagName);
        tag.Color.Should().Be(color);
        tag.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyName_ShouldThrowArgumentException(string invalidName)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => Tag.Create(invalidName));
        exception.Message.Should().Contain("Tag name cannot be null or empty");
        exception.ParamName.Should().Be("name");
    }

    [Fact]
    public void Create_WithNameWithWhitespace_ShouldTrimName()
    {
        // Arrange
        var tagName = "  Technology  ";

        // Act
        var tag = Tag.Create(tagName);

        // Assert
        tag.Name.Should().Be("Technology");
    }

    [Fact]
    public void Create_WithoutColor_ShouldUseDefaultColor()
    {
        // Arrange
        var tagName = "Technology";

        // Act
        var tag = Tag.Create(tagName);

        // Assert
        tag.Color.Should().Be(TagColor.Default);
        tag.Color.Value.Should().Be("#3B82F6");
    }

    [Fact]
    public void UpdateColor_WithValidColor_ShouldUpdateTagColor()
    {
        // Arrange
        var tag = Tag.Create("Technology");
        var newColor = TagColor.Create("#FF5733");

        // Act
        tag.UpdateColor(newColor);

        // Assert
        tag.Color.Should().Be(newColor);
        tag.Color.Value.Should().Be("#FF5733");
    }

    [Fact]
    public void UpdateColor_WithDefaultColor_ShouldUpdateToDefault()
    {
        // Arrange
        var tag = Tag.Create("Technology", TagColor.Create("#FF5733"));

        // Act
        tag.UpdateColor(TagColor.Default);

        // Assert
        tag.Color.Should().Be(TagColor.Default);
        tag.Color.Value.Should().Be("#3B82F6");
    }

    [Fact]
    public void Articles_ShouldInitializeAsEmptyCollection()
    {
        // Arrange & Act
        var tag = Tag.Create("Technology");

        // Assert
        tag.Articles.Should().NotBeNull();
        tag.Articles.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ParameterlessConstructor_ShouldCreateEmptyTag()
    {
        // Act
        var tag = new Tag();

        // Assert
        tag.Name.Should().Be(string.Empty);
        tag.Color.Should().Be(TagColor.Default);
        tag.Articles.Should().NotBeNull();
        tag.Articles.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldGenerateUuidV7Id()
    {
        // Arrange & Act
        var tag1 = Tag.Create("Tag1");
        var tag2 = Tag.Create("Tag2");

        // Assert
        tag1.Id.Should().NotBe(Guid.Empty);
        tag2.Id.Should().NotBe(Guid.Empty);
        tag1.Id.Should().NotBe(tag2.Id);
        
        // UUIDv7 should have version bits set to 0111 in the most significant 4 bits of the time_hi_and_version field
        var tag1Bytes = tag1.Id.ToByteArray();
        var tag2Bytes = tag2.Id.ToByteArray();
        
        // Check if it looks like UUIDv7 (version 7)
        (tag1Bytes[7] & 0xF0).Should().Be(0x70);
        (tag2Bytes[7] & 0xF0).Should().Be(0x70);
    }
}