using System.Security.Cryptography;
using System.Text;
namespace MiniCc.Api.Core.FeedNs.Domain;

public static class FeedIdentity
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
public enum FeedFormat { Rss, Atom }
public enum FeedStatus { Active, Paused, Unsubscribed }
