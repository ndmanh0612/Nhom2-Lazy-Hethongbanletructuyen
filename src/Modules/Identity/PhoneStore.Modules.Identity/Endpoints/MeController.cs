using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;

namespace PhoneStore.Modules.Identity.Endpoints;

[ApiController]
[Route("api/v1/me")]
public class MeController : ControllerBase
{
    private readonly IIdentityService _identityService;

    public MeController(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    /// <summary>
    /// FR-03: Xem thông tin tài khoản cá nhân
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProfile([FromHeader(Name = "X-User-Id")] long? userId, CancellationToken cancellationToken)
    {
        var uid = userId ?? 1; // Fallback mock user for testing if no auth context
        var result = await _identityService.GetProfileAsync(uid, cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-03: Cập nhật thông tin họ tên, số điện thoại
    /// </summary>
    [HttpPatch]
    public async Task<IActionResult> UpdateProfile([FromHeader(Name = "X-User-Id")] long? userId, [FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var uid = userId ?? 1;
        var result = await _identityService.UpdateProfileAsync(uid, request, cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Ok(result.Value, "Cập nhật hồ sơ thành công."));
    }

    /// <summary>
    /// FR-03: Đổi mật khẩu
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromHeader(Name = "X-User-Id")] long? userId, [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var uid = userId ?? 1;
        await _identityService.ChangePasswordAsync(uid, request, cancellationToken);
        return Ok(ApiResponse.Ok("Đổi mật khẩu thành công."));
    }

    /// <summary>
    /// FR-03: Danh sách địa chỉ nhận hàng của khách
    /// </summary>
    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses([FromHeader(Name = "X-User-Id")] long? userId, CancellationToken cancellationToken)
    {
        var uid = userId ?? 1;
        var result = await _identityService.GetAddressesAsync(uid, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AddressDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-03: Thêm địa chỉ nhận hàng
    /// </summary>
    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([FromHeader(Name = "X-User-Id")] long? userId, [FromBody] CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var uid = userId ?? 1;
        var result = await _identityService.AddAddressAsync(uid, request, cancellationToken);
        return StatusCode(201, ApiResponse<AddressDto>.Ok(result.Value, "Thêm địa chỉ thành công."));
    }
}
