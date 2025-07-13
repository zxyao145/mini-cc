using MediatR;
using MiniCc.Api.Core.UserNs.Application.DTOs;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public record CreateUserCommand(string UserName, string Password) : IRequest<Result<UserDto>>;
