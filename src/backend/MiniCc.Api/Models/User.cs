using System;
using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Models;

public class User
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
