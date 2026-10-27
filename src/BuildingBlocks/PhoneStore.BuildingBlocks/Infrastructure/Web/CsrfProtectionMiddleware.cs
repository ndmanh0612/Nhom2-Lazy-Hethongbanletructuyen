using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.BuildingBlocks.Infrastructure.Web;

/// <summary>
/// N2LHTBLTT-38: Bảo vệ CSRF cho các request thay đổi trạng thái (POST/PUT/PATCH/DELETE).
///
/// Cơ chế: server cấp một CSRF token ngẫu nhiên lưu trong cookie
/// <see cref="AuthCookies.CsrfToken"/> (đọc được bởi JavaScript) và gắn vào
/// header <see cref="AuthHeaders.CsrfToken"/> khi client gọi API. Với các
/// request không an toàn, middleware kiểm tra header có khớp cookie hay không.
///
/// Vì API dùng Bearer token trong header (không phải cookie tự động gửi),
/// CSRF về mặt kỹ thuật không áp dụng cho các endpoint đó. Tuy nhiên endpoint
/// nào dùng cookie để xác thực (ví dụ /api/v1/auth/refresh) thì bắt buộc phải
/// qua lớp bảo vệ này.
/// </summary>
public class CsrfProtectionMiddleware
{
    private const string SafeMethods = "GET,HEAD,OPTIONS,TRACE";

    private readonly RequestDelegate _next;
    private readonly ILogger<CsrfProtectionMiddleware> _logger;

    public CsrfProtectionMiddleware(RequestDelegate next, ILogger<CsrfProtectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (SafeMethods.Contains(context.Request.Method, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Chỉ các endpoint dùng cookie để xác thực mới cần CSRF token.
        // Endpoint này không nằm trong danh sách dưới thì bỏ qua kiểm tra.
        if (!RequiresCsrfToken(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var cookieToken = context.Request.Cookies[AuthCookies.CsrfToken];
        var headerToken = context.Request.Headers[AuthHeaders.CsrfToken].ToString();

        if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(headerToken))
        {
            await WriteCsrfErrorAsync(context, "Thiếu CSRF token trong request.");
            return;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(cookieToken),
                Encoding.UTF8.GetBytes(headerToken)))
        {
            _logger.LogWarning("CSRF token không khớp cho path {Path}", context.Request.Path);
            await WriteCsrfErrorAsync(context, "CSRF token không hợp lệ.");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Các endpoint dùng cookie để xác thực nên bắt buộc có CSRF token.
    /// </summary>
    private static bool RequiresCsrfToken(PathString path)
    {
        return path.StartsWithSegments("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/v1/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteCsrfErrorAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new ErrorResponse
        {
            Code = "CSRF_TOKEN_INVALID",
            Message = message,
            RequestId = context.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// N2LHTBLTT-38: Cấp CSRF token cho client.
/// </summary>
public static class CsrfTokenProvider
{
    /// <summary>
    /// Lấy CSRF token hiện có trong cookie, nếu chưa có thì sinh mới và set cookie.
    /// </summary>
    public static string GetOrCreate(HttpContext httpContext)
    {
        var existing = httpContext.Request.Cookies[AuthCookies.CsrfToken];
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        httpContext.Response.Cookies.Append(AuthCookies.CsrfToken, token, new CookieOptions
        {
            HttpOnly = false,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(8)
        });

        return token;
    }
}
