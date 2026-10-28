using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Contracts.Catalog;
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
    Task<Result<CategoryDto>> UpdateCategoryAsync(long categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<string>> DeleteCategoryAsync(long categoryId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BrandDto>>> GetBrandsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> GetBrandByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> UpdateBrandAsync(long brandId, UpdateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result<string>> DeleteBrandAsync(long brandId, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ProductDto>>> GetProductsAsync(ProductFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<ProductDetailDto>> GetProductByIdAsync(long id, CancellationToken cancellationToken = default);

    // FR-13 (N2LHTBLTT-18): CRUD sản phẩm / SKU / giá / thông số kỹ thuật.
    Task<Result<ProductDetailDto>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductDetailDto>> UpdateProductAsync(long productId, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteProductAsync(long productId, CancellationToken cancellationToken = default);
    Task<Result<ProductVariantDto>> CreateVariantAsync(long productId, CreateVariantRequest request, CancellationToken cancellationToken = default);
    Task<Result<ProductVariantDto>> UpdateVariantAsync(long variantId, UpdateVariantRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteVariantAsync(long variantId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<ProductVariantDto>>> GetVariantsAsync(VariantFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<ProductImageDto>> AddProductImageAsync(long productId, CreateProductImageRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteProductImageAsync(long imageId, CancellationToken cancellationToken = default);
}

public class CatalogService : ICatalogService
{
    private readonly CatalogDbContext _dbContext;
    private readonly IEventBus? _eventBus;

    public CatalogService(CatalogDbContext dbContext, IEventBus? eventBus = null)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
    }

    // -----------------------------------------------------------------------
    // FR-12 (N2LHTBLTT-10): Danh mục.
    // -----------------------------------------------------------------------

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

        if (category is null)
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

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(long categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FindAsync([categoryId], cancellationToken);
        if (category is null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với ID {categoryId}.", "CATEGORY_NOT_FOUND");
        }

        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ValidationException("Name", "Tên danh mục không được để trống.");
        }

        if (trimmedName.Length > 100)
        {
            throw new ValidationException("Name", "Tên danh mục không được vượt quá 100 ký tự.");
        }

        var duplicate = await _dbContext.Categories
            .AnyAsync(c => c.Name.ToLower() == trimmedName.ToLower() && c.Id != categoryId, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("Tên danh mục đã tồn tại.", "CATEGORY_ALREADY_EXISTS");
        }

        category.Update(trimmedName, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.IsActive));
    }

    public async Task<Result<string>> DeleteCategoryAsync(long categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FindAsync([categoryId], cancellationToken);
        if (category is null)
        {
            throw new NotFoundException($"Không tìm thấy danh mục với ID {categoryId}.", "CATEGORY_NOT_FOUND");
        }

        var hasProducts = await _dbContext.Products.AnyAsync(p => p.CategoryId == categoryId, cancellationToken);
        if (hasProducts)
        {
            category.Update(category.Name, isActive: false);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success("Danh mục đã được vô hiệu hóa (xóa mềm) do đang có sản phẩm tham chiếu.");
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success("Xóa danh mục thành công.");
    }

    // -----------------------------------------------------------------------
    // FR-12 (N2LHTBLTT-10): Thương hiệu.
    // -----------------------------------------------------------------------

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

        if (brand is null)
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

    public async Task<Result<BrandDto>> UpdateBrandAsync(long brandId, UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FindAsync([brandId], cancellationToken);
        if (brand is null)
        {
            throw new NotFoundException($"Không tìm thấy thương hiệu với ID {brandId}.", "BRAND_NOT_FOUND");
        }

        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new ValidationException("Name", "Tên thương hiệu không được để trống.");
        }

        if (trimmedName.Length > 100)
        {
            throw new ValidationException("Name", "Tên thương hiệu không được vượt quá 100 ký tự.");
        }

        var duplicate = await _dbContext.Brands
            .AnyAsync(b => b.Name.ToLower() == trimmedName.ToLower() && b.Id != brandId, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("Tên thương hiệu đã tồn tại.", "BRAND_ALREADY_EXISTS");
        }

        brand.Update(trimmedName, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new BrandDto(brand.Id, brand.Name, brand.IsActive));
    }

    public async Task<Result<string>> DeleteBrandAsync(long brandId, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FindAsync([brandId], cancellationToken);
        if (brand is null)
        {
            throw new NotFoundException($"Không tìm thấy thương hiệu với ID {brandId}.", "BRAND_NOT_FOUND");
        }

        var hasProducts = await _dbContext.Products.AnyAsync(p => p.BrandId == brandId, cancellationToken);
        if (hasProducts)
        {
            brand.Update(brand.Name, isActive: false);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success("Thương hiệu đã được vô hiệu hóa (xóa mềm) do đang có sản phẩm tham chiếu.");
        }

        _dbContext.Brands.Remove(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success("Xóa thương hiệu thành công.");
    }

    // -----------------------------------------------------------------------
    // FR-04 / FR-05: Truy vấn sản phẩm.
    // -----------------------------------------------------------------------

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

            return new ProductDto(p.Id, p.CategoryId, categoryName, p.BrandId, brandName, p.Name, minPrice, maxPrice, mainImage, p.IsActive, p.CreatedAt);
        }).ToList();

        return Result.Success(PagedResult<ProductDto>.Create(dtoList, page, pageSize, totalCount));
    }

    public async Task<Result<ProductDetailDto>> GetProductByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(id, includeInactive: false, cancellationToken);

        if (product == null)
        {
            throw new NotFoundException("Sản phẩm không tồn tại hoặc đã bị ẩn.", "PRODUCT_NOT_FOUND");
        }

        var category = await _dbContext.Categories.FindAsync([product.CategoryId], cancellationToken);
        var brand = await _dbContext.Brands.FindAsync([product.BrandId], cancellationToken);

        return Result.Success(ToDetailDto(product, category?.Name, brand?.Name));
    }

    // -----------------------------------------------------------------------
    // FR-13 (N2LHTBLTT-18): CRUD sản phẩm / SKU / giá / thông số.
    // -----------------------------------------------------------------------

    public async Task<Result<ProductDetailDto>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryAndBrandExistAsync(request.CategoryId, request.BrandId, cancellationToken);

        var product = Product.Create(
            request.CategoryId,
            request.BrandId,
            request.Name,
            request.Description,
            request.SpecificationsJson);

        if (request.Variants is not null && request.Variants.Count > 0)
        {
            var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenCombos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var vReq in request.Variants)
            {
                var normalizedSku = vReq.Sku.Trim().ToUpperInvariant();
                if (!seenSkus.Add(normalizedSku))
                {
                    throw new ConflictException($"Mã SKU '{normalizedSku}' bị trùng.", "SKU_ALREADY_EXISTS");
                }

                var skuExists = await _dbContext.ProductVariants
                    .AnyAsync(v => v.Sku == normalizedSku, cancellationToken);
                if (skuExists)
                {
                    throw new ConflictException("Mã SKU đã tồn tại.", "SKU_ALREADY_EXISTS");
                }

                var normalizedColor = vReq.Color?.Trim() ?? string.Empty;
                var normalizedCapacity = vReq.Capacity?.Trim() ?? string.Empty;
                var comboKey = $"{normalizedColor}|{normalizedCapacity}";
                if (!seenCombos.Add(comboKey))
                {
                    throw new ConflictException("Tổ hợp màu và dung lượng này đã tồn tại trên sản phẩm.", "DUPLICATE_VARIANT_COMBINATION");
                }

                var variant = ProductVariant.Create(
                    product.Id,
                    normalizedSku,
                    normalizedColor,
                    normalizedCapacity,
                    vReq.Price,
                    vReq.StockQuantity);

                product.AddVariant(variant);
            }
        }

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_eventBus is not null)
        {
            foreach (var variant in product.Variants)
            {
                await _eventBus.PublishAsync(new ProductVariantCreated(variant.Id, variant.ProductId, variant.Sku, variant.StockQuantity), cancellationToken);
            }
        }

        var category = await _dbContext.Categories.FindAsync([product.CategoryId], cancellationToken);
        var brand = await _dbContext.Brands.FindAsync([product.BrandId], cancellationToken);

        return Result.Success(ToDetailDto(product, category?.Name, brand?.Name));
    }

    public async Task<Result<ProductDetailDto>> UpdateProductAsync(long productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(productId, includeInactive: true, tracking: true, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm.", "PRODUCT_NOT_FOUND");
        }

        await EnsureCategoryAndBrandExistAsync(request.CategoryId, request.BrandId, cancellationToken);

        product.UpdateInfo(request.Name, request.Description, request.SpecificationsJson, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var category = await _dbContext.Categories.FindAsync([product.CategoryId], cancellationToken);
        var brand = await _dbContext.Brands.FindAsync([product.BrandId], cancellationToken);

        return Result.Success(ToDetailDto(product, category?.Name, brand?.Name));
    }

    public async Task<Result> DeleteProductAsync(long productId, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(productId, includeInactive: true, tracking: true, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm.", "PRODUCT_NOT_FOUND");
        }

        product.UpdateInfo(product.Name, product.Description, product.SpecificationsJson, isActive: false);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<ProductVariantDto>> CreateVariantAsync(long productId, CreateVariantRequest request, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(productId, includeInactive: true, tracking: true, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm.", "PRODUCT_NOT_FOUND");
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();
        var skuExists = await _dbContext.ProductVariants
            .AnyAsync(v => v.Sku == normalizedSku, cancellationToken);
        if (skuExists)
        {
            throw new ConflictException("Mã SKU đã tồn tại.", "SKU_ALREADY_EXISTS");
        }

        var normalizedColor = request.Color?.Trim() ?? string.Empty;
        var normalizedCapacity = request.Capacity?.Trim() ?? string.Empty;
        var duplicateCombination = product.Variants.Any(v =>
            string.Equals(v.Color, normalizedColor, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(v.Capacity, normalizedCapacity, StringComparison.OrdinalIgnoreCase));
        if (duplicateCombination)
        {
            throw new ConflictException(
                "Tổ hợp màu và dung lượng này đã tồn tại trên sản phẩm.",
                "DUPLICATE_VARIANT_COMBINATION");
        }

        var variant = ProductVariant.Create(
            productId,
            normalizedSku,
            normalizedColor,
            normalizedCapacity,
            request.Price,
            stockQuantity: request.StockQuantity);

        product.AddVariant(variant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_eventBus is not null)
        {
            await _eventBus.PublishAsync(new ProductVariantCreated(variant.Id, variant.ProductId, variant.Sku, request.StockQuantity), cancellationToken);
        }

        return Result.Success(ToVariantDto(variant));
    }

    public async Task<Result<ProductVariantDto>> UpdateVariantAsync(long variantId, UpdateVariantRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants.FindAsync([variantId], cancellationToken);
        if (variant is null)
        {
            throw new NotFoundException("Không tìm thấy phiên bản sản phẩm.", "VARIANT_NOT_FOUND");
        }

        variant.UpdatePrice(request.Price);
        variant.SetActive(request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToVariantDto(variant));
    }

    public async Task<Result> DeleteVariantAsync(long variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants.FindAsync([variantId], cancellationToken);
        if (variant is null)
        {
            throw new NotFoundException("Không tìm thấy phiên bản sản phẩm.", "VARIANT_NOT_FOUND");
        }

        variant.SetActive(false);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PagedResult<ProductVariantDto>>> GetVariantsAsync(VariantFilterQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 50 : query.PageSize;

        var baseQuery = _dbContext.ProductVariants.AsNoTracking();

        if (query.ProductId.HasValue)
        {
            baseQuery = baseQuery.Where(v => v.ProductId == query.ProductId.Value);
        }

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(v => v.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Sku))
        {
            var sku = query.Sku.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(v => v.Sku.Contains(sku));
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var variants = await baseQuery
            .OrderBy(v => v.ProductId)
            .ThenBy(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => ToVariantDto(v))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<ProductVariantDto>.Create(variants, page, pageSize, totalCount));
    }

    public async Task<Result<ProductImageDto>> AddProductImageAsync(long productId, CreateProductImageRequest request, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(productId, includeInactive: true, tracking: true, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm.", "PRODUCT_NOT_FOUND");
        }

        var image = ProductImage.Create(productId, request.ImageUrl, request.SortOrder);
        product.AddImage(image);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new ProductImageDto(image.Id, image.ImageUrl, image.SortOrder));
    }

    public async Task<Result> DeleteProductImageAsync(long imageId, CancellationToken cancellationToken = default)
    {
        var image = await _dbContext.ProductImages.FindAsync([imageId], cancellationToken);
        if (image is null)
        {
            throw new NotFoundException("Không tìm thấy ảnh sản phẩm.", "PRODUCT_IMAGE_NOT_FOUND");
        }

        _dbContext.ProductImages.Remove(image);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private Task<Product?> LoadProductAsync(long id, bool includeInactive, CancellationToken cancellationToken)
    {
        return LoadProductAsync(id, includeInactive, tracking: false, cancellationToken);
    }

    private Task<Product?> LoadProductAsync(long id, bool includeInactive, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Product> query = _dbContext.Products
            .Include(p => p.Variants)
            .Include(p => p.Images);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    private async Task EnsureCategoryAndBrandExistAsync(long categoryId, long brandId, CancellationToken cancellationToken)
    {
        var categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == categoryId && c.IsActive, cancellationToken);
        if (!categoryExists)
        {
            throw new ValidationException("categoryId", "Danh mục không tồn tại hoặc đã bị ẩn.");
        }

        var brandExists = await _dbContext.Brands
            .AnyAsync(b => b.Id == brandId && b.IsActive, cancellationToken);
        if (!brandExists)
        {
            throw new ValidationException("brandId", "Thương hiệu không tồn tại hoặc đã bị ẩn.");
        }
    }

    private static ProductVariantDto ToVariantDto(ProductVariant variant)
    {
        return new ProductVariantDto(
            variant.Id,
            variant.ProductId,
            variant.Sku,
            variant.Color,
            variant.Capacity,
            variant.Price,
            variant.StockQuantity,
            variant.IsActive);
    }

    private static ProductDetailDto ToDetailDto(Product product, string? categoryName, string? brandName)
    {
        var variantDtos = product.Variants
            .Where(v => v.IsActive)
            .Select(ToVariantDto)
            .ToList();

        var imageDtos = product.Images
            .OrderBy(i => i.SortOrder)
            .Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.SortOrder))
            .ToList();

        return new ProductDetailDto(
            product.Id,
            product.CategoryId,
            categoryName ?? "Chưa phân loại",
            product.BrandId,
            brandName ?? "Chưa phân loại",
            product.Name,
            product.Description,
            product.SpecificationsJson,
            product.IsActive,
            variantDtos,
            imageDtos);
    }
}
