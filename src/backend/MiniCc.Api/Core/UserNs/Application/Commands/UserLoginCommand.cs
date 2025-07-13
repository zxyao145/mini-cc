using MediatR;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public enum LoginResult
{
    Success = 0,
    Fail = 1,
    UserNotFound = 2,
}


public record UserLoginCommand(string UserName, string Password, bool RememberMe = true) : IRequest<Result<LoginResult>>;
