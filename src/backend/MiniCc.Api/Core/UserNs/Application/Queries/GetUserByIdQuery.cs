using MediatR;
using MiniCc.Api.Core.UserNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.UserNs.Application.Queries;

public record GetUserByIdQuery(Guid Id) : IRequest<Result<UserDto>>;
