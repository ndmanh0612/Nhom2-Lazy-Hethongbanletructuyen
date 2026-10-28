using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Infrastructure.Web;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;

namespace PhoneStore.Modules.Identity.Endpoints;

/// <summary>
/// FR-01 / FR-02: Đăng ký, đăng nhập, làm mới token và đăng xuất.
///
/// Access token trả trong body (client gửi kèm header Authorization: Bearer).
/// Refresh token trả qua cookie HttpOnly để JavaScript không đọc được —
/// đây là cơ chế session an toàn hơn localStorage.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly JwtOptions _options;

    public AuthController(IIdentityService identityService, IOptions<JwtOptions> options)
    {
        _identityService = identityService;
        _options = options.Value;
    }

    /// <summary>
    /// FR-01: Đăng ký tài khoản khách hàng mới.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.RegisterAsync(request, cancellationToken);
        var dto = result.Value;

        AppendAuthCookies(dto);

        return StatusCode(201, ApiResponse<AuthResponseDto>.Ok(dto, "Đăng ký tài khoản thành công."));
    }

    /// <summary>
    /// FR-02: Đăng nhập hệ thống, trả access token + refresh token (cookie).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.LoginAsync(request, cancellationToken);
        var dto = result.Value;

        AppendAuthCookies(dto);

        return Ok(ApiResponse<AuthResponseDto>.Ok(dto, "Đăng nhập thành công."));
    }

    /// <summary>
    /// FR-02: Làm mới access token bằng refresh token trong cookie.
    /// Endpoint này dùng cookie để xác thực nên bắt buộc có CSRF token
    /// (header X-CSRF-TOKEN khớp với cookie phonestore_csrf).
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[AuthCookies.RefreshToken];
        var result = await _identityService.RefreshAsync(refreshToken, cancellationToken);
        var dto = result.Value;

        AppendAuthCookies(dto);

        return Ok(ApiResponse<AuthResponseDto>.Ok(dto, "Làm mới token thành công."));
    }

    /// <summary>
    /// FR-02: Đăng xuất — thu hồi refresh token và xoá cookie phiên.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[AuthCookies.RefreshToken];
        // Cookie + CSRF vẫn cho phép logout khi JWT đã hết hạn.
        var sessionId = User.Identity?.IsAuthenticated == true ? User.FindFirst("sid")?.Value : null;
        await _identityService.LogoutAsync(refreshToken, cancellationToken, sessionId);

        Response.Cookies.Delete(AuthCookies.RefreshToken, new CookieOptions
        {
            HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict, Path = "/"
        });

        return Ok(ApiResponse.Ok("Đăng xuất thành công."));
    }

    /// <summary>
    /// N2LHTBLTT-38: Trả CSRF token cho client để dùng ở các request cần cookie.
    /// </summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult GetCsrfToken()
    {
        CsrfTokenProvider.GetOrCreate(HttpContext);
        return Ok(ApiResponse.Ok("Lấy CSRF token thành công."));
    }

    /// <summary>
    /// Đặt refresh token vào cookie HttpOnly và phát cookie CSRF token.
    /// </summary>
    private void AppendAuthCookies(AuthResponseDto dto)
    {
        Response.Cookies.Append(AuthCookies.RefreshToken, dto.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays)
        });

        CsrfTokenProvider.GetOrCreate(HttpContext);
    }
}
