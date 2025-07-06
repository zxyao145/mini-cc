using MiniCc.Api.Data;
using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Models;

public class AccessKey
{
    [Required]
    public Guid Id { get; set; } 

    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Api Key
    /// </summary>
    [Required]
    public string Name { get; set; } = "";

    /// <summary>
    /// Api Key
    /// </summary>
    [Required]
    [Sensitive]
    public string Key { get; set; } = "";


    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTimeOffset? ExpiredTime { get; set; }

    /// <summary>
    /// 是否禁用
    /// </summary>
    public bool Disabled { get; set; }

}
