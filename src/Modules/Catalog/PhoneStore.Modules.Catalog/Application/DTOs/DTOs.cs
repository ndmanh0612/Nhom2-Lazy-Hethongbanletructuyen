using System.ComponentModel.DataAnnotations;

namespace PhoneStore.Modules.Catalog.Application.DTOs;

public record CategoryDto(long Id, string Name, bool IsActive);
public record CreateCategoryRequest([Required, MaxLength(100)] string Name);
public record UpdateCategoryRequest([Required, MaxLength(100)] string Name, bool IsActive = true);

public record BrandDto(long Id, string Name, bool IsActive);
public record CreateBrandRequest([Required, MaxLength(100)] string Name);
public record UpdateBrandRequest([Required, MaxLength(100)] string Name, bool IsActive = true);

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
    bool IsActive
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
