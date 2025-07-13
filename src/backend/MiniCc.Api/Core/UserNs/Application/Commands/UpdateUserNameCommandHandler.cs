using MediatR;
using MiniCc.Api.Infra;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public class UpdateUserNameCommandCommandHandler : IRequestHandler<UpdateUserNameCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateUserNameCommandCommandHandler> _logger;
    private readonly ISignInService _signInService;

    public UpdateUserNameCommandCommandHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UpdateUserNameCommandCommandHandler> logger,
        ISignInService signInService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _signInService = signInService;
    }

    public async Task<Result> Handle(UpdateUserNameCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users
            .FirstOrDefaultAsync(x => x.UserName == request.OldUserName);
        if (user == null)
        {
            return Result<LoginResult>.Failure(LoginResult.UserNotFound, "用户不存在");
        }

        user.UpdateName(request.UserName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);


        var signUser = new SignInUser
        {
            NameIdentifier = user.Id + "",
            Name = user.UserName,
            IsPersistent = request.IsPersistent
        };
        await _signInService.SignInAsync(signUser);

        return Result<LoginResult>.Success(LoginResult.Success);
    }
}