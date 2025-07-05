using Microsoft.Extensions.Options;

namespace MiniCc.Api.Services;


public class AccessKeys
{
    public List<AccessKey> Keys { get; set; } = new();
}


public class AccessKey
{
    public string Key { get; set; } = "";
}


public interface IAccessKeyService
{
    Task<bool> IsValid(string key);
}


public class AccessKeyService: IAccessKeyService
{
    private readonly Dictionary<string, AccessKey> _accessKeys;

    public AccessKeyService(IOptions<AccessKeys> options)
    {
        _accessKeys = options.Value
            .Keys
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, x => x)
            ;
    }
    public Task<bool> IsValid(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(false);
        }
        var isValid = _accessKeys.ContainsKey(key);
        return Task.FromResult(isValid);
    }
}
