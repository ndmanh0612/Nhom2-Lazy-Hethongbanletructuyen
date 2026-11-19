using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.Modules.Identity.Endpoints;

[ApiController]
[EnableRateLimiting(IdentityRateLimiting.Policy)]
public sealed class RecoveryController(AccountRecoveryService recovery, ICurrentUser currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("api/v1/auth/forgot-password")]
    public async Task<IActionResult> Forgot(ForgotPasswordRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ChallengeDto>.Ok(await recovery.RequestAsync(request, ct)));

    [AllowAnonymous]
    [HttpPost("api/v1/auth/verify-reset-otp")]
    public async Task<IActionResult> Verify(VerifyRecoveryRequest request, CancellationToken ct) =>
        Ok(ApiResponse<RecoveryTokenDto>.Ok(await recovery.VerifyAsync(request, ct)));

    [AllowAnonymous]
    [HttpPost("api/v1/auth/reset-password")]
    public async Task<IActionResult> Reset(CompleteRecoveryRequest request, CancellationToken ct)
    {
        await recovery.CompleteAsync(request, ct);
        return Ok(ApiResponse.Ok("Đã đặt mật khẩu mới. Vui lòng đăng nhập lại."));
    }

    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    [HttpPost("api/v1/me/phone-verification")]
    public async Task<IActionResult> RequestPhone(VerifyPhoneRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ChallengeDto>.Ok(await recovery.RequestPhoneAsync(ActorId(), request, ct)));

    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    [HttpPost("api/v1/me/phone-verification/confirm")]
    public async Task<IActionResult> ConfirmPhone(ConfirmPhoneRequest request, CancellationToken ct)
    {
        await recovery.ConfirmPhoneAsync(ActorId(), request, ct);
        return Ok(ApiResponse.Ok("Đã xác minh số điện thoại."));
    }
    private long ActorId() => currentUser.UserId ?? throw new UnauthorizedException();
}
