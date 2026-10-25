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
    /// FR-12: Danh sách thương hiệu đang hoạt động
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBrands(CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetBrandsAsync(false, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BrandDto>>.Ok(result.Value));
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
}
