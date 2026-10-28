using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Application.Services;

namespace PhoneStore.Modules.Catalog.Endpoints;

/// <summary>
/// FR-12 (N2LHTBLTT-10): Quản lý thương hiệu sản phẩm.
///
/// Đọc: mở cho mọi khách hàng. Ghi (quản trị): yêu cầu vai trò STAFF hoặc ADMIN.
/// </summary>
[ApiController]
[Route("api/v1/brands")]
public class BrandsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public BrandsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>
    /// FR-12: Danh sách thương hiệu đang hoạt động.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetBrands(CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetBrandsAsync(false, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BrandDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-12: Xem chi tiết thương hiệu theo ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBrandById(long id, CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetBrandByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<BrandDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-12: Quản trị tạo thương hiệu mới.
    /// </summary>
    [HttpPost("/api/v1/admin/brands")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> CreateBrand([FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateBrandAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<BrandDto>.Ok(result.Value, "Tạo thương hiệu thành công."));
    }

    /// <summary>
    /// FR-12: Cập nhật tên / trạng thái của thương hiệu.
    /// </summary>
    [HttpPut("/api/v1/admin/brands/{brandId:long}")]
    [HttpPatch("/api/v1/admin/brands/{brandId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> UpdateBrand(
        long brandId,
        [FromBody] UpdateBrandRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.UpdateBrandAsync(brandId, request, cancellationToken);
        return Ok(ApiResponse<BrandDto>.Ok(result.Value, "Cập nhật thương hiệu thành công."));
    }

    /// <summary>
    /// FR-12: Xoá thương hiệu. Nếu còn sản phẩm thuộc thương hiệu thì thương hiệu
    /// được ẩn thay vì xoá cứng.
    /// </summary>
    [HttpDelete("/api/v1/admin/brands/{brandId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    public async Task<IActionResult> DeleteBrand(long brandId, CancellationToken cancellationToken)
    {
        var result = await _catalogService.DeleteBrandAsync(brandId, cancellationToken);
        return Ok(ApiResponse.Ok(result.Value));
    }
}
