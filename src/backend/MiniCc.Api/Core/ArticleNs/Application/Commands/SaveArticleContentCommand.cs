using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;

public record SaveArticleContentCommand(
    [Required, Url, StringLength(2000)] string Url,
    [Required] string OriginalContent,
    [Required] string ReadableContent,
    [Required, StringLength(500)] string? Title,
    [StringLength(200)] string? Author,
    [StringLength(1000)] string? Summary,
    [StringLength(2000)] string? ImageUrl
) : IRequest<Result<ArticleDto>>;
