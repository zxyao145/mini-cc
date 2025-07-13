using MiniCc.Api.Core.ApiKeys.Application.DTOs;

namespace MiniCc.Api.Core.UserNs.Application.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<ApiKeyDto> ApiKeys { get; set; } = new();
}
