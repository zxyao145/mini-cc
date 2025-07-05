namespace MiniCc.Api.Common;

public class Utils
{
    // 帮助方法：检查是否为API请求
    public static bool IsApiRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api") ||
               request.Headers["Accept"].ToString().Contains("application/json") ||
               request.Headers["Content-Type"].ToString().Contains("application/json");
    }
}
