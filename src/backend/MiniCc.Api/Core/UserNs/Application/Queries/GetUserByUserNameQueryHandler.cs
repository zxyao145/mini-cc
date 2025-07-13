using MediatR;
using MiniCc.Api.Core.UserNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.UserNs.Application.Queries;

public class GetUserByUserNameQueryHandler : IRequestHandler<GetUserByUserNameQuery, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetUserByUserNameQueryHandler> _logger;

    public GetUserByUserNameQueryHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GetUserByUserNameQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<UserDto>> Handle(GetUserByUserNameQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByUserNameAsync(request.UserName, cancellationToken);
            if (user == null)
            {
                return Result<UserDto>.Failure("User not found");
            }

            return Result<UserDto>.Success(_mapper.Map<UserDto>(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by username: {UserName}", request.UserName);
            return Result<UserDto>.Failure($"Failed to get user: {ex.Message}");
        }
    }
}
