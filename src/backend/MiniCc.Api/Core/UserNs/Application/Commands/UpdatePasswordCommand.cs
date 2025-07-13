using MediatR;
using MiniCc.Api.Shared;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public class UpdatePasswordCommand : IRequest<Result>
{
    [JsonIgnore]
    public string UserName { get; set; } = "";

    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = string.Empty;
}
