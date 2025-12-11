using Microsoft.AspNetCore.Http;

namespace Utilities
{
    public class RequestTypeHelper
    {
        public static bool CheckRequestType(HttpRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "HTTP request cannot be null");

            if (request.Headers.TryGetValue("User-Agent", out var userAgent))
            {
                if (userAgent.ToString().ToLower().Contains("postman"))
                    return false;
            }

            if (request.Headers.TryGetValue("Sec-Fetch-Mode", out var mode))
            {
                if (mode.ToString().ToLower() == "cors")
                    return false;
            }

            if (request.Headers.TryGetValue("Referer", out var referer))
            {
                if (referer.ToString().ToLower().Contains("swagger"))
                    return false;
            }
            return true;
        }
    }
}
