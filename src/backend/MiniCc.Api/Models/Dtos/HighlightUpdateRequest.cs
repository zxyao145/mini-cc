namespace MiniCc.Api.Models.Dtos;

public class HighlightUpdateRequest
{
    /// <summary>
    /// 高亮文本
    /// </summary>
    public string? Text { get; set; }
    
    /// <summary>
    /// 高亮备注
    /// </summary>
    public string? Note { get; set; }
    
    /// <summary>
    /// 高亮颜色
    /// </summary>
    public string? Color { get; set; }
    
    /// <summary>
    /// 开始位置偏移
    /// </summary>
    public int? StartOffset { get; set; }
    
    /// <summary>
    /// 结束位置偏移
    /// </summary>
    public int? EndOffset { get; set; }
}