using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Catalog.Application.DTOs;
using PhoneStore.Modules.Catalog.Domain.Entities;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;

namespace PhoneStore.Modules.Catalog.Application.Services;

public interface ICatalogService
{
    Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<BrandDto>>> GetBrandsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<ProductDto>>> GetProductsAsync(ProductFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<ProductDetailDto>> GetProductByIdAsync(long id, CancellationToken cancellationToken = default);
}

public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _dbContext;

    public CatalogService(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CategoryDto>>(categories);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name.Trim();
        var exists = await _dbContext.Categories.AnyAsync(c => c.Name == trimmedName, cancellationToken);
        if (exists)
        {
            throw new ConflictException("Tên danh mục đã tồn tại.", "CATEGORY_ALREADY_EXISTS");
        }

        var category = Category.Create(trimmedName);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.IsActive));
    }

    public async Task<Result<IReadOnlyList<BrandDto>>> GetBrandsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Brands.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        var brands = await query
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Id, b.Name, b.IsActive))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BrandDto>>(brands);
    }

    public async Task<Result<BrandDto>> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name.Trim();
        var exists = await _dbContext.Brands.AnyAsync(b => b.Name == trimmedName, cancellationToken);
        if (exists)
        {
            throw new ConflictException("Tên thương hiệu đã tồn tại.", "BRAND_ALREADY_EXISTS");
        }

        var brand = Brand.Create(trimmedName);
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new BrandDto(brand.Id, brand.Name, brand.IsActive));
    }

    public async Task<Result<PagedResult<ProductDto>>> GetProductsAsync(ProductFilterQuery query, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (query.Page < 1)
        {
            errors["Page"] = ["Số trang phải lớn hơn hoặc bằng 1."];
        }

        if (query.PageSize is < 1 or > 50)
        {
            errors["PageSize"] = ["Số lượng phần tử trên mỗi trang phải từ 1 đến 50."];
        }

        if (query.MinPrice.HasValue && query.MinPrice.Value < 0)
        {
            errors["MinPrice"] = ["Giá tối thiểu không được nhỏ hơn 0."];
        }

        if (query.MaxPrice.HasValue && query.MaxPrice.Value < 0)
        {
            errors["MaxPrice"] = ["Giá tối đa không được nhỏ hơn 0."];
        }

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue &&
            query.MinPrice.Value >= 0 && query.MaxPrice.Value >= 0 &&
            query.MinPrice.Value > query.MaxPrice.Value)
        {
            errors["MaxPrice"] = ["Giá tối đa phải lớn hơn hoặc bằng giá tối thiểu."];
        }

        var normalizedSort = query.SortBy?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalizedSort) &&
            normalizedSort != "newest" &&
            normalizedSort != "price_asc" &&
            normalizedSort != "price_desc")
        {
            errors["SortBy"] = ["Kiểu sắp xếp không hợp lệ. Chỉ chấp nhận: newest, price_asc, price_desc."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var page = query.Page;
        var pageSize = query.PageSize;

        var baseQuery = _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(p => p.Name.ToLower().Contains(search));
        }

        if (query.CategoryId.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.BrandId.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.BrandId == query.BrandId.Value);
        }

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.Variants.Any(v => v.IsActive && v.Price >= query.MinPrice.Value && v.Price <= query.MaxPrice.Value));
        }
        else if (query.MinPrice.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.Variants.Any(v => v.IsActive && v.Price >= query.MinPrice.Value));
        }
        else if (query.MaxPrice.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.Variants.Any(v => v.IsActive && v.Price <= query.MaxPrice.Value));
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return Result.Success(PagedResult<ProductDto>.Create([], page, pageSize, 0));
        }

        switch (normalizedSort)
        {
            case "price_asc":
                baseQuery = baseQuery
                    .OrderBy(p => p.Variants.Where(v => v.IsActive).Min(v => (long?)v.Price) ?? long.MaxValue)
                    .ThenBy(p => p.Id);
                break;

            case "price_desc":
                baseQuery = baseQuery
                    .OrderByDescending(p => p.Variants.Where(v => v.IsActive).Min(v => (long?)v.Price) ?? -1)
                    .ThenByDescending(p => p.Id);
                break;

            default:
                baseQuery = baseQuery
                    .OrderByDescending(p => p.CreatedAt)
                    .ThenByDescending(p => p.Id);
                break;
        }

        var products = await baseQuery
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var categoryIds = products.Select(p => p.CategoryId).Distinct().ToList();
        var brandIds = products.Select(p => p.BrandId).Distinct().ToList();

        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var brands = await _dbContext.Brands
            .AsNoTracking()
            .Where(b => brandIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        var dtoList = products.Select(p =>
        {
            var activeVariants = p.Variants.Where(v => v.IsActive).ToList();
            var minPrice = activeVariants.Count != 0 ? activeVariants.Min(v => v.Price) : 0;
            var maxPrice = activeVariants.Count != 0 ? activeVariants.Max(v => v.Price) : 0;
            var mainImage = p.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.ImageUrl;
            var categoryName = categories.GetValueOrDefault(p.CategoryId, "Chưa phân loại");
            var brandName = brands.GetValueOrDefault(p.BrandId, "Chưa phân loại");

            return new ProductDto(p.Id, p.CategoryId, categoryName, p.BrandId, brandName, p.Name, minPrice, maxPrice, mainImage, p.IsActive);
        }).ToList();

        return Result.Success(PagedResult<ProductDto>.Create(dtoList, page, pageSize, totalCount));
    }

    public async Task<Result<ProductDetailDto>> GetProductByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException("Sản phẩm không tồn tại hoặc đã bị ẩn.", "PRODUCT_NOT_FOUND");
        }

        var category = await _dbContext.Categories.FindAsync([product.CategoryId], cancellationToken);
        var brand = await _dbContext.Brands.FindAsync([product.BrandId], cancellationToken);

        var variantDtos = product.Variants
            .Where(v => v.IsActive)
            .Select(v => new ProductVariantDto(v.Id, v.ProductId, v.Sku, v.Color, v.Capacity, v.Price, v.StockQuantity, v.IsActive))
            .ToList();

        var imageDtos = product.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.SortOrder))
            .ToList();

        var detail = new ProductDetailDto(
            product.Id,
            product.CategoryId,
            category?.Name ?? "Chưa phân loại",
            product.BrandId,
            brand?.Name ?? "Chưa phân loại",
            product.Name,
            product.Description,
            product.SpecificationsJson,
            product.IsActive,
            variantDtos,
            imageDtos
        );

        return Result.Success(detail);
    }
}
