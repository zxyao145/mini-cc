using FluentValidation;
using MediatR;
using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Shared;
using MiniCc.Api.Shared.Data.Repositories;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniCc.Api.Core.UserNs.Application.Commands;

public class UpdateReaderPreferencesCommand : IRequest<Result<ReaderPreferences>>
{
    [JsonIgnore] public string UserName { get; set; } = "";
    public int? FontSize { get; set; }
    public double? LineHeight { get; set; }
    public string? Width { get; set; }
    public string? FontFamily { get; set; }
    public string? Theme { get; set; }
}

public class UpdateReaderPreferencesCommandValidator : AbstractValidator<UpdateReaderPreferencesCommand>
{
    public UpdateReaderPreferencesCommandValidator()
    {
        RuleFor(c => c.FontSize).NotNull().InclusiveBetween(16, 28);
        RuleFor(c => c.LineHeight).NotNull().InclusiveBetween(1.5, 2.2);
        RuleFor(c => c.Width).NotNull().Must(v => v is "narrow" or "standard" or "wide");
        RuleFor(c => c.FontFamily).NotNull().Must(v => v is "system" or "serif");
        RuleFor(c => c.Theme).NotNull().Must(v => v is "system" or "light" or "dark" or "sepia");
    }
}

public class UpdateReaderPreferencesCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateReaderPreferencesCommand, Result<ReaderPreferences>>
{
    public async Task<Result<ReaderPreferences>> Handle(UpdateReaderPreferencesCommand request, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);
        if (user == null) return Result<ReaderPreferences>.Failure("用户不存在");
        var preferences = new ReaderPreferences { FontSize=request.FontSize!.Value, LineHeight=request.LineHeight!.Value,
            Width=request.Width!, FontFamily=request.FontFamily!, Theme=request.Theme! };
        user.UpdateReaderPreferences(JsonSerializer.Serialize(preferences, JsonSerializerOptions.Web));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ReaderPreferences>.Success(preferences);
    }
}
