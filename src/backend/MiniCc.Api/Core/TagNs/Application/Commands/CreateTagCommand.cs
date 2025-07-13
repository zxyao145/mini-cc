using MediatR;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.TagNs.Application.Commands;

public record CreateTagCommand(string Name, string? Color): IRequest<Result<Tag>>;
