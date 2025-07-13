using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;

namespace MiniCc.Api.Core.ApiKeys.Application.DTOs;

public class CreateApiKeyResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
    public ApiKey? ApiKey { get; set; }
}


