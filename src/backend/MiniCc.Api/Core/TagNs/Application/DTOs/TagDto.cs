namespace MiniCc.Api.Core.TagNs.Application.DTOs;

public class TagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
