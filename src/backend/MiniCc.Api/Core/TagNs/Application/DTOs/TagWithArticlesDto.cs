using MiniCc.Api.Core.ArticleNs.Application.DTOs;

namespace MiniCc.Api.Core.TagNs.Application.DTOs;

public class TagWithArticlesDto : TagDto
{
    public List<ArticleLightDto> Articles { get; set; } = new();
}