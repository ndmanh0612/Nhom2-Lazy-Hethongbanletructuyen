using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Application.Services;

namespace PhoneStore.Modules.Catalog.Endpoints;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public ProductsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    /// <summary>
    /// FR-04: Danh sách sản phẩm, lọc theo danh mục, thương hiệu, khoảng giá, tìm kiếm
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetProductsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<ProductDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-05: Xem chi tiết sản phẩm, danh sách phiên bản và ảnh
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetProductById(long id, CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetProductByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ProductDetailDto>.Ok(result.Value));
    }
}
