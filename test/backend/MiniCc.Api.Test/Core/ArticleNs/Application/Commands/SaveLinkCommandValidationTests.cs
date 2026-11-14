using MiniCc.Api.Core.ArticleNs.Application.Commands;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Commands;

public class SaveLinkCommandValidationTests
{
    public static TheoryData<SaveLinkCommand, string?> Inputs => new()
    {
        { new("https://example.com/v", "Title", null, null, null), null },
        { new("HTTP://example.com/v", "Title", null, null, null), null },
        { new("ftp://example.com/v", "Title", null, null, null), "Url" },
        { new("https://", "Title", null, null, null), "Url" },
        { new("https://example.com/v", "", null, null, null), "Title" },
        { new("https://example.com/v", "  ", null, null, null), "Title" },
        { new("https://example.com/v", new string('t', 501), null, null, null), "Title" },
        { new("https://example.com/" + new string('a', 2000), "Title", null, null, null), "Url" },
        { new("https://example.com/v", "Title", new string('a', 201), null, null), "Author" },
        { new("https://example.com/v", "Title", null, new string('s', 1001), null), "Summary" },
        { new("https://example.com/v", "Title", null, null, new string('i', 2001)), "ImageUrl" }
    };

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Validator_ValidatesLinkMetadata(SaveLinkCommand command, string? invalidField)
    {
        var result = new SaveLinkCommandValidator().Validate(command);

        result.IsValid.Should().Be(invalidField == null);
        if (invalidField != null)
            result.Errors.Should().Contain(error => error.PropertyName == invalidField);
    }
}
