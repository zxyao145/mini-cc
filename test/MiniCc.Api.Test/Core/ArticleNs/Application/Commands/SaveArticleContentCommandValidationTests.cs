using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MiniCc.Api.Core.ArticleNs.Application.Commands;

namespace MiniCc.Api.Test.Core.ArticleNs.Application.Commands;

public class SaveArticleContentCommandValidationTests
{
    [Theory]
    [InlineData("<p>Readable</p>", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void MvcValidation_RequiresExtractedReadableContent(string? readableContent, bool valid)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var context = new ActionContext(new DefaultHttpContext { RequestServices = provider },
            new RouteData(), new ActionDescriptor());
        var command = new SaveArticleContentCommand("https://example.com/article", "<html>Original</html>",
            readableContent!, "Title", null, null, null);

        // Exercise MVC's record validation metadata, rather than calling the controller directly.
        provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, "", command);

        context.ModelState.IsValid.Should().Be(valid);
        if (!valid)
            context.ModelState.Should().ContainKey(nameof(command.ReadableContent));
    }
}
