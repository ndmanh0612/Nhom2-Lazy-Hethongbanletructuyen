using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Catalog.Application.Services;

namespace PhoneStore.Modules.Catalog.Endpoints;

/// <summary>Đánh giá sản phẩm và danh sách yêu thích.</summary>
[ApiController]
public class EngagementController : ControllerBase
{
    private readonly IEngagementService _service;
    private readonly ICurrentUser _currentUser;

    public EngagementController(IEngagementService service, ICurrentUser currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("api/v1/products/{productId:long}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviews(long productId, CancellationToken ct)
        => Ok(ApiResponse<ReviewSummaryDto>.Ok(await _service.GetReviewsAsync(productId, ct)));

    [HttpPut("api/v1/products/{productId:long}/reviews")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> UpsertReview(long productId, [FromBody] UpsertReviewRequest request, CancellationToken ct)
        => Ok(ApiResponse<ReviewDto>.Ok(await _service.UpsertReviewAsync(productId, RequireUserId(), request, ct), "Đã lưu đánh giá."));

    [HttpDelete("api/v1/products/{productId:long}/reviews")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> DeleteReview(long productId, CancellationToken ct)
    {
        await _service.DeleteReviewAsync(productId, RequireUserId(), ct);
        return NoContent();
    }

    [HttpGet("api/v1/me/wishlist")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> GetWishlist(CancellationToken ct)
        => Ok(ApiResponse<IReadOnlyList<long>>.Ok(await _service.GetWishlistAsync(RequireUserId(), ct)));

    [HttpPut("api/v1/me/wishlist/{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> AddToWishlist(long productId, CancellationToken ct)
    {
        await _service.AddToWishlistAsync(RequireUserId(), productId, ct);
        return NoContent();
    }

    [HttpDelete("api/v1/me/wishlist/{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
    public async Task<IActionResult> RemoveFromWishlist(long productId, CancellationToken ct)
    {
        await _service.RemoveFromWishlistAsync(RequireUserId(), productId, ct);
        return NoContent();
    }

    private long RequireUserId()
        => _currentUser.UserId ?? throw new UnauthorizedAccessException("Bạn cần đăng nhập.");
}
