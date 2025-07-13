using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.ApiKeys.Application.Commands;

public class DeleteApiKeyRequest
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    [Required]
    public Guid Id { get; set; }
}
