using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;

namespace MiniCc.Api.Test.Core.TagNs.Domain.AggregatesModel.ValueObjects;

public class TagColorTests
{
    [Fact]
    public void Create_WithValidHexColor_ShouldReturnTagColorValueObject()
    {
        // Arrange
        var validColor = "#FF5733";

        // Act
        var tagColor = TagColor.Create(validColor);

        // Assert
        tagColor.Value.Should().Be(validColor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyColor_ShouldReturnDefault(string invalidColor)
    {
        // Act
        var tagColor = TagColor.Create(invalidColor);

        // Assert
        tagColor.Should().Be(TagColor.Default);
        tagColor.Value.Should().Be("#3B82F6");
    }

    [Theory]
    [InlineData("FF5733")]        // Missing #
    [InlineData("#FF57")]         // Too short
    [InlineData("#FF5733AA")]     // Too long
    [InlineData("#GG5733")]       // Invalid hex character
    [InlineData("#ff57zz")]       // Invalid hex character
    [InlineData("not-a-color")]   // Not a color format
    public void Create_WithInvalidHexColor_ShouldThrowArgumentException(string invalidColor)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => TagColor.Create(invalidColor));
        exception.Message.Should().Contain("Invalid color format. Must be a valid hex color.");
        exception.ParamName.Should().Be("color");
    }

    [Theory]
    [InlineData("#FF5733")]
    [InlineData("#000000")]
    [InlineData("#FFFFFF")]
    [InlineData("#ff5733")]  // lowercase
    [InlineData("#AbCdEf")]  // mixed case
    [InlineData("#123456")]
    [InlineData("#ABCDEF")]
    public void Create_WithValidHexColors_ShouldSucceed(string validColor)
    {
        // Act
        var tagColor = TagColor.Create(validColor);

        // Assert
        tagColor.Value.Should().Be(validColor);
    }

    [Fact]
    public void Default_ShouldReturnBlueColor()
    {
        // Act & Assert
        TagColor.Default.Value.Should().Be("#3B82F6");
    }

    [Fact]
    public void ImplicitConversion_ShouldReturnColorValue()
    {
        // Arrange
        var colorValue = "#FF5733";
        var tagColor = TagColor.Create(colorValue);

        // Act
        string convertedColor = tagColor;

        // Assert
        convertedColor.Should().Be(colorValue);
    }

    [Fact]
    public void ToString_ShouldReturnColorValue()
    {
        // Arrange
        var colorValue = "#FF5733";
        var tagColor = TagColor.Create(colorValue);

        // Act
        var result = tagColor.ToString();

        // Assert
        result.Should().Be(colorValue);
    }

    [Fact]
    public void Equality_WithSameColor_ShouldBeEqual()
    {
        // Arrange
        var colorValue = "#FF5733";
        var color1 = TagColor.Create(colorValue);
        var color2 = TagColor.Create(colorValue);

        // Act & Assert
        color1.Should().Be(color2);
        (color1 == color2).Should().BeTrue();
        color1.GetHashCode().Should().Be(color2.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentColors_ShouldNotBeEqual()
    {
        // Arrange
        var color1 = TagColor.Create("#FF5733");
        var color2 = TagColor.Create("#33FF57");

        // Act & Assert
        color1.Should().NotBe(color2);
        (color1 == color2).Should().BeFalse();
    }
}