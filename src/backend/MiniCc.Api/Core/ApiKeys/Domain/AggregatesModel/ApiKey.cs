using MiniCc.Api.Core.UserNs.Domain.AggregatesModel;
using MiniCc.Api.Shared.Data;
using MiniCc.Api.Shared.Data.Common;
using System.ComponentModel.DataAnnotations;

namespace MiniCc.Api.Core.ApiKeys.Domain.AggregatesModel;

public class ApiKey : BaseAuditableEntity
{
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Api Key
    /// </summary>
    [Required]
    public string Name { get; set; } = "";

    /// <summary>
    /// Api Key
    /// </summary>
    [Required]
    [Sensitive]
    public string Key { get; set; } = "";


    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTimeOffset? ExpiredTime { get; set; }

    /// <summary>
    /// 是否禁用
    /// </summary>
    public bool Disabled { get; set; }


    public virtual User User { get; private set; }

    private ApiKey(Guid id, Guid userId, string name, string key, DateTimeOffset? expiredTime) : base(id)
    {
        UserId = userId;
        Name = name;
        Key = key;
        ExpiredTime = expiredTime;
        Disabled = false;
    }

    public ApiKey() : base()
    {
    }

    public static ApiKey Create(Guid userId, string name, DateTimeOffset? expiredTime = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty", nameof(name));

        var id = UuidUtil.NewGuidV7();
        var key = GenerateApiKey();

        return new ApiKey(id, userId, name, key, expiredTime);
    }

    public void Disable()
    {
        Disabled = true;
    }

    public void Enable()
    {
        Disabled = false;
    }

    public bool IsValid()
    {
        if (Disabled)
            return false;

        if (ExpiredTime.HasValue && ExpiredTime.Value < DateTimeOffset.UtcNow)
            return false;

        return true;
    }

    public void UpdateExpiration(DateTimeOffset? newExpiredTime)
    {
        ExpiredTime = newExpiredTime;
    }

    private static string GenerateApiKey()
    {
        return Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    }
}