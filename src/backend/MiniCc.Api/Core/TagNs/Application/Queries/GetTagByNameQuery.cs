using MediatR;
using MiniCc.Api.Core.TagNs.Application.DTOs;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Shared;

namespace MiniCc.Api.Core.TagNs.Application.Queries;

public record GetTagByNameQuery(
    string Name
) : IRequest<Result<Tag?>>;