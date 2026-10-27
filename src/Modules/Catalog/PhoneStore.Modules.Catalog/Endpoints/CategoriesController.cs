using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Application.Services;

namespace PhoneStore.Modules.Catalog.Endpoints;

/// <summary>
/// FR-12 (N2LHTBLTT-10): Quản lý danh mục sản phẩm.
///
/// Đọc: mở cho mọi khách hàng. Ghi (quản trị): yêu cầu vai trò STAFF hoặc ADMIN.
/// </summary>
[ApiController]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CategoriesController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>
    /// FR-12: Danh sách danh mục đang hoạt động.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetCategoriesAsync(false, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-12: Quản trị tạo danh mục mới.
    /// </summary>
    [HttpPost("/api/v1/admin/categories")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateCategoryAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<CategoryDto>.Ok(result.Value, "Tạo danh mục thành công."));
    }

    /// <summary>
    /// FR-12: Cập nhật tên / trạng thái của danh mục.
    /// </summary>
    [HttpPut("/api/v1/admin/categories/{categoryId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> UpdateCategory(
        long categoryId,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.UpdateCategoryAsync(categoryId, request, cancellationToken);
        return Ok(ApiResponse<CategoryDto>.Ok(result.Value, "Cập nhật danh mục thành công."));
    }

    /// <summary>
    /// FR-12: Xoá danh mục. Nếu còn sản phẩm thuộc danh mục thì danh mục được ẩn
    /// thay vì xoá cứng, để giữ tính toàn vẹn dữ liệu lịch sử đơn hàng.
    /// </summary>
    [HttpDelete("/api/v1/admin/categories/{categoryId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> DeleteCategory(long categoryId, CancellationToken cancellationToken)
    {
        await _catalogService.DeleteCategoryAsync(categoryId, cancellationToken);
        return Ok(ApiResponse.Ok("Xoá danh mục thành công."));
    }
}
