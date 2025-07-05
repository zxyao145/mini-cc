using MiniCc.Api.Controllers;
using System.ComponentModel.DataAnnotations;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace MiniCc.Api.Services;


public class UserLoginCommand
{
    [Required]
    public string Username { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";

    /// <summary>
    /// 记住我选项
    /// </summary>
    public bool RememberMe { get; set; } = true;
}

public enum LoginResult
{
    Success = 0,
    Fail = 1,
}

public interface IAccountService
{
    Task<LoginResult> LoginAsync(UserLoginCommand command);
}

public class AccountService : IAccountService
{
    public Task<LoginResult> LoginAsync(UserLoginCommand command)
    {
        var userName = Environment.GetEnvironmentVariable("MiniCC_UserName") ?? "demo";
        var pwd = Environment.GetEnvironmentVariable("MiniCC_Password") ?? "demo_password";

        if (command.Username != userName || command.Password != pwd)
        {
            return Task.FromResult(LoginResult.Fail);
        }
        return Task.FromResult(LoginResult.Success);
    }
}
