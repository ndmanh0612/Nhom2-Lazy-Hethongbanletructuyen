using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.BuildingBlocks.Infrastructure.Identity;

/// <summary>
/// N2LHTBLTT-9: Đọc danh tính người dùng từ claims của principal đã xác thực.
/// Không còn fallback đọc header "X-User-Id" — danh tính phải đến từ JWT hợp lệ.
/// </summary>
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
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");

            return long.TryParse(subject, out var userId) ? userId : null;
        }
    }

    public string? Role
    {
        get
        {
            var principal = _httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return principal.FindFirstValue(ClaimTypes.Role)
                ?? principal.FindFirstValue("role");
        }
    }

    public bool IsAuthenticated => UserId.HasValue;

    public bool IsInRole(string role)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return principal.IsInRole(role);
    }
}
