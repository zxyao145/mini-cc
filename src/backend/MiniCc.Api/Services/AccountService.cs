using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MiniCc.Api.Common;
using MiniCc.Api.Controllers;
using MiniCc.Api.Data;
using MiniCc.Api.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace MiniCc.Api.Services;


public class UserLoginCommand
{
    [Required]
    public string UserName { get; set; } = "";
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
    UserNoFound = 2,
}

public class UpdateResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
}

public interface IAccountService
{
    Task<LoginResult> LoginAsync(UserLoginCommand command);
    Task<User?> FindByUserName(string userName);
    Task<UpdateResult> UpdateUserNameAsync(string currentUserName, string newUserName);
    Task<UpdateResult> UpdatePasswordAsync(string userName, string currentPassword, string newPassword);
}

public class AccountService : IAccountService
{
    private readonly MiniCcContext _context;
    private readonly ILogger<AccountService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountService(MiniCcContext context, ILogger<AccountService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }



    public async Task<LoginResult> LoginAsync(UserLoginCommand command)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x=>x.UserName == command.UserName);
        if(user == null)
        {
            return LoginResult.UserNoFound;
        }

        if (!PasswordUtil.VerifyHashedPassword(user.Password, command.Password))
        {
            return LoginResult.Fail;
        }


        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = command.RememberMe,
        };

        await _httpContextAccessor.HttpContext!
            .SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity), 
                authProperties
            );

        return LoginResult.Success;
    }


    public async Task<User?> FindByUserName(string userName)
    {
        var user = await _context.Users
           .FirstOrDefaultAsync(x => x.UserName == userName);
        return user;
    }

    public async Task<UpdateResult> UpdateUserNameAsync(string currentUserName, string newUserName)
    {
        var user = await FindByUserName(currentUserName);
        if (user == null)
        {
            return new UpdateResult { Success = false, ErrorMessage = "用户未找到" };
        }

        // 检查新用户名是否已存在
        var existingUser = await FindByUserName(newUserName);
        if (existingUser != null && existingUser.Id != user.Id)
        {
            return new UpdateResult { Success = false, ErrorMessage = "用户名已存在" };
        }

        user.UserName = newUserName;
        await _context.SaveChangesAsync();

        return new UpdateResult { Success = true };
    }

    public async Task<UpdateResult> UpdatePasswordAsync(string userName, string currentPassword, string newPassword)
    {
        var user = await FindByUserName(userName);
        if (user == null)
        {
            return new UpdateResult { Success = false, ErrorMessage = "用户未找到" };
        }

        // 验证当前密码
        if (!PasswordUtil.VerifyHashedPassword(user.Password, currentPassword))
        {
            return new UpdateResult { Success = false, ErrorMessage = "当前密码错误" };
        }

        // 更新密码
        user.Password = PasswordUtil.HashPassword(newPassword);
        await _context.SaveChangesAsync();

        return new UpdateResult { Success = true };
    }
}
