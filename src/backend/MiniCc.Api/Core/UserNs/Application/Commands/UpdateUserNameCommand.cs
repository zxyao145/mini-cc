using MediatR;
using MiniCc.Api.Shared;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public class UpdateUserNameCommand : IRequest<Result>
{
    [Required]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "用户名长度必须在2-20字符之间")]
    public string UserName { get; set; } = "";


    [JsonIgnore]
    public string OldUserName { get; set; } = "";

    [JsonIgnore]
    public bool IsPersistent { get; set; }
}
