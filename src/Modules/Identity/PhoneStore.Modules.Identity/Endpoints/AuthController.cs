using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Infrastructure.Web;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;

namespace PhoneStore.Modules.Identity.Endpoints;

/// <summary>
/// FR-01 / FR-02 / FR-09: Đăng ký, đăng nhập, làm mới token và đăng xuất.
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

    public AuthController(IIdentityService identityService)
    {
        _identityService = identityService;
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
    /// FR-02 / FR-09: Làm mới access token bằng refresh token trong cookie.
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
    /// FR-02 / FR-09: Đăng xuất — thu hồi refresh token và xoá cookie phiên.
    /// </summary>
    [HttpPost("logout")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[AuthCookies.RefreshToken];
        await _identityService.LogoutAsync(refreshToken, cancellationToken);

        Response.Cookies.Delete(AuthCookies.RefreshToken);

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
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        CsrfTokenProvider.GetOrCreate(HttpContext);
    }
}
