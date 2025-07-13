using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ApiKeys.Application.Commands;

public class CreateApiKeyRequest
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "名称长度必须在1-50字符之间")]
    public string Name { get; set; } = "";

    public DateTimeOffset? ExpiredTime { get; set; }
}
