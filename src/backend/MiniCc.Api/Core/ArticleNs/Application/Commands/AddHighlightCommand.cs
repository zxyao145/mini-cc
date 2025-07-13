using MediatR;
using MiniCc.Api.Core.ArticleNs.Application.DTOs;
using MiniCc.Api.Shared;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ArticleNs.Application.Commands;


public class AddHighlightCommand : IRequest<Result<HighlightDto>>
{
    [JsonIgnore]
    public Guid ArticleId { get; set; } = Guid.Empty;

    /// <summary>
    /// 高亮文本内容
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// 高亮备注
    /// </summary>
    public string Note { get; set; } = "";

    /// <summary>
    /// 高亮颜色
    /// </summary>
    public string Color { get; set; } = "";

    /// <summary>
    /// 开始位置偏移
    /// </summary>
    public int StartOffset { get; set; }

    /// <summary>
    /// 结束位置偏移
    /// </summary>
    public int EndOffset { get; set; }
}