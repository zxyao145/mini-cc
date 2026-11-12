using MediatR;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;
using System.Text.Json;

namespace MiniCc.Api.Core.UserNs.Application.Queries;

public record GetReaderPreferencesQuery(string UserName) : IRequest<Result<ReaderPreferences>>;

public class GetReaderPreferencesQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetReaderPreferencesQuery, Result<ReaderPreferences>>
{
    public async Task<Result<ReaderPreferences>> Handle(GetReaderPreferencesQuery request, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);
        if (user == null) return Result<ReaderPreferences>.Failure("用户不存在");
        return Result<ReaderPreferences>.Success(user.ReaderPreferences == null ? new() :
            JsonSerializer.Deserialize<ReaderPreferences>(user.ReaderPreferences, JsonSerializerOptions.Web) ?? new());
    }
}
