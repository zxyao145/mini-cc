using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;
using MiniCc.Api.Shared.Utils;
using System.Security.Claims;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public class UserLoginCommandHandler : IRequestHandler<UserLoginCommand, Result<LoginResult>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserLoginCommandHandler> _logger;
    private readonly ISignInService _signInService;

    public UserLoginCommandHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UserLoginCommandHandler> logger,
        IHttpContextAccessor httpContextAccessor,
        ISignInService signInService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _signInService = signInService;
    }

    public async Task<Result<LoginResult>> Handle(UserLoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users
            .FirstOrDefaultAsync(x => x.UserName == request.UserName);
        if (user == null)
        {
            return Result<LoginResult>.Failure(LoginResult.UserNotFound, "用户不存在");
        }

        if (!PasswordUtil.VerifyHashedPassword(user.Password, request.Password))
        {
            return Result<LoginResult>.Failure(LoginResult.Fail, "密码错误");
        }

        var signUser = new SignInUser
        {
            NameIdentifier = user.Id + "",
            Name = user.UserName,
            IsPersistent = request.RememberMe
        };

        await _signInService.SignInAsync(signUser);

        return Result<LoginResult>.Success(LoginResult.Success);
    }
}