using MediatR;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;

namespace MiniCc.Api.Core.TagNs.Application.Queries;

public class GetTagByNameQueryHandler : IRequestHandler<GetTagByNameQuery, Result<Tag?>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetTagByNameQueryHandler> _logger;

    public GetTagByNameQueryHandler(IUnitOfWork unitOfWork, ILogger<GetTagByNameQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Tag?>> Handle(GetTagByNameQuery request, CancellationToken cancellationToken)
    {
        var tag = await _unitOfWork
            .Tags
            .GetByNameAsync(request.Name);
        return Result<Tag?>.Success(tag);
    }
}
