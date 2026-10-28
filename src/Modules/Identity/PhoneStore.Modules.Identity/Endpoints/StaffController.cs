using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;

namespace PhoneStore.Modules.Identity.Endpoints;

/// <summary>
/// FR-15 (N2LHTBLTT-16): Quản lý tài khoản nhân viên — tạo, danh sách, đổi vai trò,
/// khóa/mở khóa và đặt lại mật khẩu.
///
/// Chỉ ADMIN được gọi các endpoint quản trị tài khoản nhân viên.
/// </summary>
[ApiController]
[Route("api/v1/admin/staff")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
public class StaffController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUser _currentUser;

    public StaffController(IIdentityService identityService, ICurrentUser currentUser)
    {
        _identityService = identityService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// FR-15: Tạo tài khoản nhân viên mới (STAFF hoặc ADMIN).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.CreateStaffAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<UserProfileDto>.Ok(result.Value, "Tạo tài khoản nhân viên thành công."));
    }

    /// <summary>
    /// FR-15: Danh sách tài khoản nhân viên, có lọc và phân trang.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStaff([FromQuery] StaffFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await _identityService.GetStaffAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<UserProfileDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-15: Đổi vai trò của một tài khoản (STAFF ↔ ADMIN).
    /// </summary>
    [HttpPatch("{userId:long}/role")]
    public async Task<IActionResult> ChangeRole(
        long userId,
        [FromBody] ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _identityService.ChangeRoleAsync(userId, request, RequireActorId(), cancellationToken);
        return Ok(ApiResponse<UserProfileDto>.Ok(result.Value, "Đổi vai trò thành công."));
    }

    /// <summary>
    /// FR-15: Khóa / mở khóa tài khoản. Khoá tài khoản sẽ thu hồi mọi phiên đang mở.
    /// </summary>
    [HttpPatch("{userId:long}/lock")]
    public async Task<IActionResult> LockAccount(
        long userId,
        [FromBody] LockAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _identityService.LockAccountAsync(userId, request, RequireActorId(), cancellationToken);
        var message = request.IsLocked ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản.";
        return Ok(ApiResponse<UserProfileDto>.Ok(result.Value, message));
    }

    /// <summary>
    /// FR-15: Đặt lại mật khẩu cho một tài khoản.
    /// </summary>
    [HttpPost("{userId:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        long userId,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _identityService.ResetPasswordAsync(userId, request, cancellationToken);
        return Ok(ApiResponse.Ok("Đặt lại mật khẩu thành công."));
    }

    private long RequireActorId()
    {
        return _currentUser.UserId
            ?? throw new UnauthorizedException("Vui lòng đăng nhập để sử dụng chức năng này.");
    }
}
