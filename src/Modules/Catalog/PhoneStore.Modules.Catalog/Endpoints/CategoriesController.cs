using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Application.Services;

namespace PhoneStore.Modules.Catalog.Endpoints;

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
    /// FR-12: Danh sách danh mục (mặc định chỉ lấy danh mục đang hoạt động)
    /// </summary>
    [HttpGet]
    [HttpGet("/api/v1/admin/categories")]
    public async Task<IActionResult> GetCategories([FromQuery] bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var result = await _catalogService.GetCategoriesAsync(includeInactive, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Ok(result.Value));
    }

    /// <summary>
    /// FR-12: Chi tiết danh mục theo ID
    /// </summary>
    [HttpGet("{id:long}")]
    [HttpGet("/api/v1/admin/categories/{id:long}")]
    public async Task<IActionResult> GetCategoryById(long id, CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetCategoryByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<CategoryDto>.Ok(result.Value));
    }

    /// <summary>
    /// FR-12: Quản trị tạo danh mục mới
    /// </summary>
    [HttpPost("/api/v1/admin/categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.CreateCategoryAsync(request, cancellationToken);
        return StatusCode(201, ApiResponse<CategoryDto>.Ok(result.Value, "Tạo danh mục thành công."));
    }

    /// <summary>
    /// FR-12: Quản trị cập nhật danh mục
    /// </summary>
    [HttpPut("/api/v1/admin/categories/{id:long}")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.UpdateCategoryAsync(id, request, cancellationToken);
        return Ok(ApiResponse<CategoryDto>.Ok(result.Value, "Cập nhật danh mục thành công."));
    }

    /// <summary>
    /// FR-12: Quản trị xóa hoặc vô hiệu hóa danh mục
    /// </summary>
    [HttpDelete("/api/v1/admin/categories/{id:long}")]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken cancellationToken)
    {
        var result = await _catalogService.DeleteCategoryAsync(id, cancellationToken);
        return Ok(ApiResponse.Ok(result.Value));
    }
}
