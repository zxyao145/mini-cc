using FluentValidation;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record SaveLinkCommand(string Url, string Title, string? Author, string? Summary, string? ImageUrl)
    : IRequest<Result<ArticleDto>>;

public class SaveLinkCommandValidator : AbstractValidator<SaveLinkCommand>
{
    public SaveLinkCommandValidator()
    {
        RuleFor(c => c.Url).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(2000).Must(IsHttpUrl);
        RuleFor(c => c.Title).NotEmpty().MaximumLength(500);
        RuleFor(c => c.Author).MaximumLength(200);
        RuleFor(c => c.Summary).MaximumLength(1000);
        RuleFor(c => c.ImageUrl).MaximumLength(2000);
    }

    private static bool IsHttpUrl(string url)
    {
        return Uri.IsWellFormedUriString(url, UriKind.Absolute) &&
               new Uri(url).Scheme is "http" or "https";
    }
}
