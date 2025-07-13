using MediatR;
using MiniCc.Api.Core.UserNs.Application.DTOs;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CreateUserCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existingUser = await _unitOfWork.Users.GetByUserNameAsync(request.UserName, cancellationToken);
            if (existingUser != null)
            {
                return Result<UserDto>.Failure("Username already exists");
            }

            var user = User.Create(request.UserName, request.Password);
            var savedUser = await _unitOfWork.Users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<UserDto>.Success(_mapper.Map<UserDto>(savedUser));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user: {UserName}", request.UserName);
            return Result<UserDto>.Failure($"Failed to create user: {ex.Message}");
        }
    }
}
