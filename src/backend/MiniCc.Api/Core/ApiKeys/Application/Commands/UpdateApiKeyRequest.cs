using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ApiKeys.Application.Commands;

public class UpdateApiKeyRequest
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "名称长度必须在1-50字符之间")]
    public string Name { get; set; } = "";

    public DateTimeOffset? ExpiredTime { get; set; }

    public bool Disabled { get; set; }
}
