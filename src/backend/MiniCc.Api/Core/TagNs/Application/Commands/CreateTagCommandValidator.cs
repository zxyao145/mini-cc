using FluentValidation;
using MiniCc.Api.Core.TagNs.Domain.AggregatesModel.ValueObjects;

namespace MiniCc.Api.Core.TagNs.Application.Commands;

public class CreateTagCommandValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagCommandValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100)
            .WithMessage("标签名称必须为 1–100 个字符");

        RuleFor(request => request.Color)
            .Must(TagColor.IsValidHexColor)
            .When(request => !string.IsNullOrWhiteSpace(request.Color))
            .WithMessage("颜色必须是 #RRGGBB 格式");
    }
}
