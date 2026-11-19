using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;

namespace PhoneStore.Modules.Identity.Endpoints;

[ApiController]
[Route("api/v1/admin/customers")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public sealed class CustomersController(IdentityService identity, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] CustomerFilterQuery query, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<UserProfileDto>>.Ok(await identity.GetCustomersAsync(query, ct)));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        Ok(ApiResponse<CustomerDetailDto>.Ok(await identity.GetCustomerAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Create(RegisterRequest request, CancellationToken ct) =>
        StatusCode(201, ApiResponse<UserProfileDto>.Ok(await identity.CreateCustomerAsync(request, ActorId(), ct)));

    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateCustomerRequest request, CancellationToken ct) =>
        Ok(ApiResponse<UserProfileDto>.Ok(await identity.UpdateCustomerAsync(id, request, ActorId(), ct)));

    [HttpPatch("{id:long}/lock")]
    public async Task<IActionResult> Lock(long id, LockAccountRequest request, CancellationToken ct) =>
        Ok(ApiResponse<UserProfileDto>.Ok(await identity.SetCustomerLockAsync(id, request, ActorId(), ct)));

    [HttpPost("{id:long}/revoke-sessions")]
    public async Task<IActionResult> Revoke(long id, CancellationToken ct)
    {
        await identity.RevokeCustomerSessionsAsync(id, ActorId(), ct);
        return Ok(ApiResponse.Ok("Đã thu hồi các phiên và mã khôi phục của khách hàng."));
    }

    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, ResetPasswordRequest request, CancellationToken ct)
    {
        await identity.ResetCustomerPasswordAsync(id, request, ActorId(), ct);
        return Ok(ApiResponse.Ok("Đã đặt lại mật khẩu và thu hồi mọi phiên của khách hàng."));
    }
    private long ActorId() => currentUser.UserId ?? throw new UnauthorizedException();
}
