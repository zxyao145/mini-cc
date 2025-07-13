using MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data.Common;
using MiniCc.Api.Shared.Utils;

namespace MiniCc.Api.Core.UserNs.Domain.AggregatesModel;

public class User: BaseAuditableEntity
{
    public string UserName { get; private set; } = "";
    public string Password { get; private set; } = "";

    private readonly List<ApiKey> _apiKeys = new();
    public IReadOnlyCollection<ApiKey> ApiKeys => _apiKeys.AsReadOnly();

    private User(Guid id, string userName, string password)
    {
        this.Id = id;
        UserName = userName;
        Password = password;
    }

    public User()
    {
    }

    public static User Create(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("Username cannot be null or empty", nameof(userName));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        var id = UuidUtil.NewGuidV7();
        return new User(id, userName, password);
    }

    public void UpdateName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("UserName cannot be null or empty", nameof(userName));

        UserName = userName;
    }

    public void UpdatePassword(string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            throw new ArgumentException("Password cannot be null or empty", nameof(newPassword));

        Password = PasswordUtil.HashPassword(newPassword);
    }

    public ApiKey CreateApiKey(string name, DateTimeOffset? expiredTime = null)
    {
        var apiKey = ApiKey.Create(Id, name, expiredTime);
        _apiKeys.Add(apiKey);
        return apiKey;
    }

    public void RevokeApiKey(Guid apiKeyId)
    {
        var apiKey = _apiKeys.FirstOrDefault(ak => ak.Id == apiKeyId);
        if (apiKey != null)
        {
            apiKey.Disable();
        }
    }
}