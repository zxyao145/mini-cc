using FluentValidation;
using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public class UpdateArticleStateCommand : IRequest<Result<ArticleListItemDto>>
{
    [JsonIgnore]
    public Guid ArticleId { get; set; }
    public bool? IsFavorite { get; set; }
    public bool? IsArchived { get; set; }
    public string? ReadingStatus { get; set; }
}

public class UpdateArticleStateCommandValidator : AbstractValidator<UpdateArticleStateCommand>
{
    public UpdateArticleStateCommandValidator()
    {
        RuleFor(c => c.ArticleId).NotEmpty();
        RuleFor(c => c).Must(c => c.IsFavorite.HasValue || c.IsArchived.HasValue || c.ReadingStatus != null)
            .WithMessage("State patch must contain at least one value.");
        RuleFor(c => c.ReadingStatus).Must(s => s == null || s == "unread" || s == "read")
            .WithMessage("Reading status must be unread or read.");
    }
}
