using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PhoneStore.BuildingBlocks.Application;

namespace PhoneStore.BuildingBlocks.Infrastructure.Identity;

public class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long? UserId
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");
                if (long.TryParse(subject, out var userId))
                {
                    return userId;
                }
            }

            // Fallback tạm thời cho môi trường chưa có JWT thật.
            var header = _httpContextAccessor.HttpContext?.Request.Headers["X-User-Id"].ToString();
            if (long.TryParse(header, out var headerUserId))
            {
                return headerUserId;
            }

            return null;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;
}
