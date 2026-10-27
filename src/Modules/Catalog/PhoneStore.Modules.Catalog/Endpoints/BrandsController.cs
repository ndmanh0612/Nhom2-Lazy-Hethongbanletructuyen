using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Application.Services;

namespace PhoneStore.Modules.Catalog.Endpoints;

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
    /// FR-12: Danh sách thương hiệu (mặc định chỉ lấy thương hiệu đang hoạt động)
    /// </summary>
    [HttpGet]
    [HttpGet("/api/v1/admin/brands")]
    public async Task<IActionResult> GetBrands([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var result = await _catalogService.GetBrandsAsync(includeInactive, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BrandDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-12: Chi tiết thương hiệu theo ID
    /// </summary>
    [HttpGet("{id:long}")]
    [HttpGet("/api/v1/admin/brands/{id:long}")]
    public async Task<IActionResult> GetBrandById(long id, CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetBrandByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<BrandDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-12: Quản trị tạo thương hiệu mới
    /// </summary>
    [HttpPost("/api/v1/admin/brands")]
    public async Task<IActionResult> CreateBrand([FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateBrandAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<BrandDto>.Ok(result.Value, "Tạo thương hiệu thành công."));
    }

    /// <summary>
    /// FR-12: Quản trị cập nhật thương hiệu
    /// </summary>
    [HttpPut("/api/v1/admin/brands/{id:long}")]
    public async Task<IActionResult> UpdateBrand(long id, [FromBody] UpdateBrandRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.UpdateBrandAsync(id, request, cancellationToken);
        return Ok(ApiResponse<BrandDto>.Ok(result.Value, "Cập nhật thương hiệu thành công."));
    }

    /// <summary>
    /// FR-12: Quản trị xóa hoặc vô hiệu hóa thương hiệu
    /// </summary>
    [HttpDelete("/api/v1/admin/brands/{id:long}")]
    public async Task<IActionResult> DeleteBrand(long id, CancellationToken cancellationToken)
    {
        var result = await _catalogService.DeleteBrandAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(result.Value));
    }
}
