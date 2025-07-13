using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;
using MiniCc.Api.Shared.Utils;
using System.Security.Claims;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public class UpdatePasswordCommandHandler : IRequestHandler<UpdatePasswordCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UpdatePasswordCommandHandler> _logger;


    public UpdatePasswordCommandHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UpdatePasswordCommandHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result> Handle(UpdatePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users
            .FirstOrDefaultAsync(x => x.UserName == request.UserName);
        if (user == null)
        {
            return Result<LoginResult>.Failure(LoginResult.UserNotFound, "用户不存在");
        }

        if (!PasswordUtil.VerifyHashedPassword(user.Password, request.CurrentPassword))
        {
            return Result<LoginResult>.Failure(LoginResult.Fail, "密码错误");
        }

        user.UpdatePassword(request.NewPassword);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<LoginResult>.Success(LoginResult.Success);
    }
}