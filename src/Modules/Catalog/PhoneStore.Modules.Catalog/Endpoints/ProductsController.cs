using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Application.Services;

namespace PhoneStore.Modules.Catalog.Endpoints;

/// <summary>
/// FR-04 / FR-05: Truy vấn danh sách và chi tiết sản phẩm (mở cho mọi khách hàng).
/// FR-13 (N2LHTBLTT-18): CRUD sản phẩm, SKU, giá và thông số kỹ thuật (quản trị).
/// </summary>
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
    [AllowAnonymous]
    public async Task<IActionResult> GetProducts([FromQuery] ProductFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetProductsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<ProductDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-05: Xem chi tiết sản phẩm, danh sách phiên bản và ảnh
    /// </summary>
    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProductById(long id, CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetProductByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<ProductDetailDto>.Ok(result.Value));
    }

    // -----------------------------------------------------------------------
    // FR-13 (N2LHTBLTT-18): Quản trị sản phẩm / SKU / giá / thông số kỹ thuật.
    // -----------------------------------------------------------------------

    /// <summary>
    /// FR-13: Tạo sản phẩm mới kèm các phiên bản (SKU) ban đầu.
    /// </summary>
    [HttpPost("/api/v1/admin/products")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateProductAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<ProductDetailDto>.Ok(result.Value, "Tạo sản phẩm thành công."));
    }

    /// <summary>
    /// FR-13: Cập nhật thông tin sản phẩm (danh mục, thương hiệu, tên, mô tả, thông số).
    /// </summary>
    [HttpPut("/api/v1/admin/products/{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> UpdateProduct(
        long productId,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.UpdateProductAsync(productId, request, cancellationToken);
        return Ok(ApiResponse<ProductDetailDto>.Ok(result.Value, "Cập nhật sản phẩm thành công."));
    }

    /// <summary>
    /// FR-13: Xoá sản phẩm (ản nếu đã phát sinh dữ liệu liên quan).
    /// </summary>
    [HttpDelete("/api/v1/admin/products/{productId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> DeleteProduct(long productId, CancellationToken cancellationToken)
    {
        await _catalogService.DeleteProductAsync(productId, cancellationToken);
        return Ok(ApiResponse.Ok("Xoá sản phẩm thành công."));
    }

    /// <summary>
    /// FR-13: Danh sách phiên bản (SKU), có lọc theo SKU / sản phẩm / trạng thái.
    /// </summary>
    [HttpGet("/api/v1/admin/products/variants")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> GetVariants([FromQuery] VariantFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetVariantsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<ProductVariantDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-13: Tạo một phiên bản (SKU) mới cho sản phẩm.
    /// </summary>
    [HttpPost("/api/v1/admin/products/{productId:long}/variants")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> CreateVariant(
        long productId,
        [FromBody] CreateVariantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateVariantAsync(productId, request, cancellationToken);
        return StatusCode(201, ApiResponse<ProductVariantDto>.Ok(result.Value, "Tạo phiên bản sản phẩm thành công."));
    }

    /// <summary>
    /// FR-13: Cập nhật phiên bản (SKU) — giá, màu, dung lượng, trạng thái.
    /// </summary>
    [HttpPut("/api/v1/admin/products/variants/{variantId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> UpdateVariant(
        long variantId,
        [FromBody] UpdateVariantRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.UpdateVariantAsync(variantId, request, cancellationToken);
        return Ok(ApiResponse<ProductVariantDto>.Ok(result.Value, "Cập nhật phiên bản sản phẩm thành công."));
    }

    /// <summary>
    /// FR-13: Xoá phiên bản (SKU) — ẩn đi nếu đã phát sinh đơn hàng.
    /// </summary>
    [HttpDelete("/api/v1/admin/products/variants/{variantId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> DeleteVariant(long variantId, CancellationToken cancellationToken)
    {
        await _catalogService.DeleteVariantAsync(variantId, cancellationToken);
        return Ok(ApiResponse.Ok("Xoá phiên bản sản phẩm thành công."));
    }

    /// <summary>
    /// FR-13: Thêm ảnh sản phẩm.
    /// </summary>
    [HttpPost("/api/v1/admin/products/{productId:long}/images")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> AddImage(
        long productId,
        [FromBody] CreateProductImageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _catalogService.AddProductImageAsync(productId, request, cancellationToken);
        return StatusCode(201, ApiResponse<ProductImageDto>.Ok(result.Value, "Thêm ảnh sản phẩm thành công."));
    }

    /// <summary>
    /// FR-13: Xoá ảnh sản phẩm.
    /// </summary>
    [HttpDelete("/api/v1/admin/products/images/{imageId:long}")]
    [Authorize(Policy = AuthorizationPolicies.RequireStaff)]
    [ApiExplorerSettings(IgnoreApi = true)] // Tạm ẩn trên Swagger cho Sprint 1 (bật lại ở Sprint 2)
    public async Task<IActionResult> DeleteImage(long imageId, CancellationToken cancellationToken)
    {
        await _catalogService.DeleteProductImageAsync(imageId, cancellationToken);
        return Ok(ApiResponse.Ok("Xoá ảnh sản phẩm thành công."));
    }
}
