namespace PhoneStore.Modules.Catalog.Application.DTOs;

// N2LHTBLTT-14: Validation được thực hiện bằng FluentValidation (CatalogValidators),
// không dùng DataAnnotations để tránh chạy validation hai lần.

public record CategoryDto(long Id, string Name, bool IsActive);
public record CreateCategoryRequest(string Name);

/// <summary>FR-12 (N2LHTBLTT-10): Cập nhật danh mục.</summary>
public record UpdateCategoryRequest(string Name, bool IsActive);

public record BrandDto(long Id, string Name, bool IsActive);
public record CreateBrandRequest(string Name);

/// <summary>FR-12 (N2LHTBLTT-10): Cập nhật thương hiệu.</summary>
public record UpdateBrandRequest(string Name, bool IsActive);

public record ProductVariantDto(
    long Id,
    long ProductId,
    string Sku,
    string Color,
    string Capacity,
    long Price,
    int StockQuantity,
    bool IsActive
);

public record ProductImageDto(long Id, string ImageUrl, int SortOrder);

public record ProductDto(
    long Id,
    long CategoryId,
    string CategoryName,
    long BrandId,
    string BrandName,
    string Name,
    long MinPrice,
    long MaxPrice,
    string? MainImageUrl,
    bool IsActive,
    DateTimeOffset CreatedAt
);

public record ProductDetailDto(
    long Id,
    long CategoryId,
    string CategoryName,
    long BrandId,
    string BrandName,
    string Name,
    string Description,
    string SpecificationsJson,
    bool IsActive,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductImageDto> Images
);

public record ProductFilterQuery(
    string? Search,
    long? CategoryId,
    long? BrandId,
    long? MinPrice,
    long? MaxPrice,
    string? SortBy, // price_asc, price_desc, newest
    int Page = 1,
    int PageSize = 20
);

// ---------------------------------------------------------------------------
// FR-13 (N2LHTBLTT-18): CRUD sản phẩm, SKU, giá và thông số kỹ thuật.
// ---------------------------------------------------------------------------

/// <summary>FR-13: Tạo sản phẩm mới kèm các phiên bản (SKU) ban đầu.</summary>
public record CreateProductRequest(
    long CategoryId,
    long BrandId,
    string Name,
    string Description,
    string? SpecificationsJson,
    IReadOnlyList<CreateVariantRequest>? Variants);

/// <summary>FR-13: Cập nhật thông tin sản phẩm (không đổi SKU).</summary>
public record UpdateProductRequest(
    long CategoryId,
    long BrandId,
    string Name,
    string Description,
    string? SpecificationsJson,
    bool IsActive);

/// <summary>FR-13: Tạo một phiên bản (SKU) của sản phẩm.</summary>
public record CreateVariantRequest(
    string Sku,
    string Color,
    string Capacity,
    long Price,
    int StockQuantity);

/// <summary>FR-13: Cập nhật phiên bản (SKU) — giá, thuộc tính, trạng thái.</summary>
public record UpdateVariantRequest(
    string Sku,
    string Color,
    string Capacity,
    long Price,
    bool IsActive);

/// <summary>FR-13: Thêm ảnh sản phẩm.</summary>
public record CreateProductImageRequest(string ImageUrl, int SortOrder);

/// <summary>FR-13: Lọc danh sách phiên bản theo SKU.</summary>
public record VariantFilterQuery(string? Sku, long? ProductId, bool? IsActive, int Page = 1, int PageSize = 20);
