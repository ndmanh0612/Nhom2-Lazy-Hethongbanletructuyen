using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;

namespace PhoneStore.Modules.Identity.Endpoints;

/// <summary>
/// FR-03 (N2LHTBLTT-15): Quản lý hồ sơ, mật khẩu và địa chỉ của người dùng đang đăng nhập.
///
/// Danh tính lấy từ JWT (claim "sub"), không còn nhận từ header tạm thời.
/// </summary>
[ApiController]
[Route("api/v1/me")]
[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
[ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
public class MeController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUser _currentUser;

    public MeController(IIdentityService identityService, ICurrentUser currentUser)
    {
        _identityService = identityService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// FR-03: Xem thông tin tài khoản cá nhân.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var result = await _identityService.GetProfileAsync(RequireUserId(), cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-03: Cập nhật thông tin họ tên, số điện thoại.
    /// </summary>
    [HttpPatch]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.UpdateProfileAsync(RequireUserId(), request, cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Ok(result.Value, "Cập nhật hồ sơ thành công."));
    }

    /// <summary>
    /// FR-03: Đổi mật khẩu. Mọi phiên đang mở sẽ bị thu hồi.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _identityService.ChangePasswordAsync(RequireUserId(), request, cancellationToken);
        return Ok(ApiResponse.Ok("Đổi mật khẩu thành công."));
    }

    /// <summary>
    /// FR-03: Danh sách địa chỉ nhận hàng của khách.
    /// </summary>
    [HttpGet("addresses")]
    public async Task<IActionResult> GetAddresses(CancellationToken cancellationToken)
    {
        var result = await _identityService.GetAddressesAsync(RequireUserId(), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AddressDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-03: Thêm địa chỉ nhận hàng.
    /// </summary>
    [HttpPost("addresses")]
    public async Task<IActionResult> AddAddress([FromBody] CreateAddressRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.AddAddressAsync(RequireUserId(), request, cancellationToken);
        return StatusCode(201, ApiResponse<AddressDto>.Ok(result.Value, "Thêm địa chỉ thành công."));
    }

    private long RequireUserId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedException("Vui lòng đăng nhập để sử dụng chức năng này.");
    }
}
