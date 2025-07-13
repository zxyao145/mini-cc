namespace MiniCc.Api.Core.ApiKeys.Application.DTOs;

public class ApiKeyDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public DateTimeOffset? ExpiredTime { get; set; }
    public bool Disabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}