using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record UpdateArticleCommand : IRequest<Result<ArticleDto>>
{
    [JsonIgnore]
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    public bool? IsFavorite { get; set; }
    public bool? IsArchived { get; set; }
}
