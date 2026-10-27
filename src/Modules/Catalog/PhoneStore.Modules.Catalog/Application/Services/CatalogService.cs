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
    Task<Result<CategoryDto>> GetCategoryByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<CategoryDto>> UpdateCategoryAsync(long id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<string>> DeleteCategoryAsync(long id, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BrandDto>>> GetBrandsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> GetBrandByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> UpdateBrandAsync(long id, UpdateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result<string>> DeleteBrandAsync(long id, CancellationToken cancellationToken = default);

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

    public async Task<Result<CategoryDto>> GetCategoryByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với ID {id}.", "CATEGORY_NOT_FOUND");
        }

        return Result.Success(new CategoryDto(category.Id, category.Name, category.IsActive));
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ValidationException("Name", "Tên danh mục không được để trống.");
        }

        if (trimmedName.Length > 100)
        {
            throw new ValidationException("Name", "Tên danh mục không được vượt quá 100 ký tự.");
        }

        var exists = await _dbContext.Categories.AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
        if (exists)
        {
            throw new ConflictException("Tên danh mục đã tồn tại.", "CATEGORY_ALREADY_EXISTS");
        }

        var category = Category.Create(trimmedName);
        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.IsActive));
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(long id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ValidationException("Name", "Tên danh mục không được để trống.");
        }

        if (trimmedName.Length > 100)
        {
            throw new ValidationException("Name", "Tên danh mục không được vượt quá 100 ký tự.");
        }

        var category = await _dbContext.Categories.FindAsync([id], cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với ID {id}.", "CATEGORY_NOT_FOUND");
        }

        var duplicateExists = await _dbContext.Categories.AnyAsync(c => c.Id != id && c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
        if (duplicateExists)
        {
            throw new ConflictException("Tên danh mục đã tồn tại trên hệ thống.", "CATEGORY_ALREADY_EXISTS");
        }

        category.Update(trimmedName, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.IsActive));
    }

    public async Task<Result<string>> DeleteCategoryAsync(long id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FindAsync([id], cancellationToken);
        if (category == null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với ID {id}.", "CATEGORY_NOT_FOUND");
        }

        var hasProducts = await _dbContext.Products.AnyAsync(p => p.CategoryId == id, cancellationToken);
        if (hasProducts)
        {
            category.Update(category.Name, false);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success("Danh mục đã được vô hiệu hóa (xóa mềm) do đang có sản phẩm tham chiếu.");
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success("Xóa danh mục thành công.");
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

    public async Task<Result<BrandDto>> GetBrandByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (brand == null)
        {
            throw new NotFoundException($"Không tìm thấy thương hiệu với ID {id}.", "BRAND_NOT_FOUND");
        }

        return Result.Success(new BrandDto(brand.Id, brand.Name, brand.IsActive));
    }

    public async Task<Result<BrandDto>> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ValidationException("Name", "Tên thương hiệu không được để trống.");
        }

        if (trimmedName.Length > 100)
        {
            throw new ValidationException("Name", "Tên thương hiệu không được vượt quá 100 ký tự.");
        }

        var exists = await _dbContext.Brands.AnyAsync(b => b.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
        if (exists)
        {
            throw new ConflictException("Tên thương hiệu đã tồn tại.", "BRAND_ALREADY_EXISTS");
        }

        var brand = Brand.Create(trimmedName);
        _dbContext.Brands.Add(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new BrandDto(brand.Id, brand.Name, brand.IsActive));
    }

    public async Task<Result<BrandDto>> UpdateBrandAsync(long id, UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ValidationException("Name", "Tên thương hiệu không được để trống.");
        }

        if (trimmedName.Length > 100)
        {
            throw new ValidationException("Name", "Tên thương hiệu không được vượt quá 100 ký tự.");
        }

        var brand = await _dbContext.Brands.FindAsync([id], cancellationToken);
        if (brand == null)
        {
            throw new NotFoundException($"Không tìm thấy thương hiệu với ID {id}.", "BRAND_NOT_FOUND");
        }

        var duplicateExists = await _dbContext.Brands.AnyAsync(b => b.Id != id && b.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
        if (duplicateExists)
        {
            throw new ConflictException("Tên thương hiệu đã tồn tại trên hệ thống.", "BRAND_ALREADY_EXISTS");
        }

        brand.Update(trimmedName, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new BrandDto(brand.Id, brand.Name, brand.IsActive));
    }

    public async Task<Result<string>> DeleteBrandAsync(long id, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FindAsync([id], cancellationToken);
        if (brand == null)
        {
            throw new NotFoundException($"Không tìm thấy thương hiệu với ID {id}.", "BRAND_NOT_FOUND");
        }

        var hasProducts = await _dbContext.Products.AnyAsync(p => p.BrandId == id, cancellationToken);
        if (hasProducts)
        {
            brand.Update(brand.Name, false);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success("Thương hiệu đã được vô hiệu hóa (xóa mềm) do đang có sản phẩm tham chiếu.");
        }

        _dbContext.Brands.Remove(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success("Xóa thương hiệu thành công.");
    }

    public async Task<Result<PagedResult<ProductDto>>> GetProductsAsync(ProductFilterQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 50 ? 20 : query.PageSize;

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

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var categories = await _dbContext.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var brands = await _dbContext.Brands.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        var products = await baseQuery
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

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
