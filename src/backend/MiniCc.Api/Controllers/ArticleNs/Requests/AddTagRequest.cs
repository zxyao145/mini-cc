namespace MiniCc.Api.Controllers.ArticleNs.Requests;

public class AddTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}