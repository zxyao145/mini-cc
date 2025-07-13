using MediatR;
using MiniCc.Api.Core.UserNs.Application.DTOs;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.UserNs.Application.Queries;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<GetUserByIdQueryHandler> _logger;

    public GetUserByIdQueryHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<GetUserByIdQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<UserDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.Id, cancellationToken);
            if (user == null)
            {
                return Result<UserDto>.Failure("User not found");
            }

            return Result<UserDto>.Success(_mapper.Map<UserDto>(user));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {Id}", request.Id);
            return Result<UserDto>.Failure($"Failed to get user: {ex.Message}");
        }
    }
}
