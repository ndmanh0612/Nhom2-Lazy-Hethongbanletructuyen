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
    Task<Result<CategoryDto>> UpdateCategoryAsync(long categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteCategoryAsync(long categoryId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BrandDto>>> GetBrandsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> CreateBrandAsync(CreateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result<BrandDto>> UpdateBrandAsync(long brandId, UpdateBrandRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteBrandAsync(long brandId, CancellationToken cancellationToken = default);

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

    public CatalogService(CatalogDbContext dbContext)
    {
        _dbContext = dbContext;
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

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(long categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FindAsync([categoryId], cancellationToken);
        if (category is null)
        {
            throw new NotFoundException("Không tìm thấy danh mục.", "CATEGORY_NOT_FOUND");
        }

        var trimmedName = request.Name.Trim();

        // Tên danh mục là duy nhất — kiểm tra trùng với danh mục khác.
        var duplicate = await _dbContext.Categories
            .AnyAsync(c => c.Name == trimmedName && c.Id != categoryId, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("Tên danh mục đã tồn tại.", "CATEGORY_ALREADY_EXISTS");
        }

        category.Update(trimmedName, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.IsActive));
    }

    public async Task<Result> DeleteCategoryAsync(long categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FindAsync([categoryId], cancellationToken);
        if (category is null)
        {
            throw new NotFoundException("Không tìm thấy danh mục.", "CATEGORY_NOT_FOUND");
        }

        // Không xoá cứng nếu còn sản phẩm thuộc danh mục — dữ liệu lịch sử đơn hàng
        // cần giữ lại danh mục. Thay vào đó ẩn danh mục đi.
        var hasProducts = await _dbContext.Products.AnyAsync(p => p.CategoryId == categoryId, cancellationToken);
        if (hasProducts)
        {
            category.Update(category.Name, isActive: false);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        _dbContext.Categories.Remove(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
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

    public async Task<Result<BrandDto>> UpdateBrandAsync(long brandId, UpdateBrandRequest request, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FindAsync([brandId], cancellationToken);
        if (brand is null)
        {
            throw new NotFoundException("Không tìm thấy thương hiệu.", "BRAND_NOT_FOUND");
        }

        var trimmedName = request.Name.Trim();

        var duplicate = await _dbContext.Brands
            .AnyAsync(b => b.Name == trimmedName && b.Id != brandId, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("Tên thương hiệu đã tồn tại.", "BRAND_ALREADY_EXISTS");
        }

        brand.Update(trimmedName, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new BrandDto(brand.Id, brand.Name, brand.IsActive));
    }

    public async Task<Result> DeleteBrandAsync(long brandId, CancellationToken cancellationToken = default)
    {
        var brand = await _dbContext.Brands.FindAsync([brandId], cancellationToken);
        if (brand is null)
        {
            throw new NotFoundException("Không tìm thấy thương hiệu.", "BRAND_NOT_FOUND");
        }

        var hasProducts = await _dbContext.Products.AnyAsync(p => p.BrandId == brandId, cancellationToken);
        if (hasProducts)
        {
            brand.Update(brand.Name, isActive: false);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        _dbContext.Brands.Remove(brand);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // FR-04 / FR-05: Truy vấn sản phẩm.
    // -----------------------------------------------------------------------

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

        // Lọc theo khoảng giá của các phiên bản đang bán: sản phẩm được giữ lại
        // nếu có ít nhất một phiên bản nằm trong khoảng [MinPrice, MaxPrice].
        var hasPriceFilter = query.MinPrice.HasValue || query.MaxPrice.HasValue;
        if (hasPriceFilter)
        {
            var minPrice = query.MinPrice ?? 0;
            var maxPrice = query.MaxPrice ?? long.MaxValue;

            baseQuery = baseQuery.Where(p => p.Variants.Any(v =>
                v.IsActive && v.Price >= minPrice && v.Price <= maxPrice));
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var categories = await _dbContext.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var brands = await _dbContext.Brands.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        // Sắp xếp: theo giá sản phẩm dựa trên giá biến thể thấp nhất, hoặc theo thời gian tạo.
        // Luôn có tie-break theo Id để thứ tự ổn định khi nhiều bản ghi có cùng
        // giá / cùng thời điểm tạo (không phụ thuộc thứ tự trả về của database).
        var products = baseQuery
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .AsEnumerable();

        products = query.SortBy switch
        {
            "price_asc" => products
                .OrderBy(p => p.Variants.Where(v => v.IsActive).Select(v => (long?)v.Price).Min() ?? long.MaxValue)
                .ThenBy(p => p.Id),
            "price_desc" => products
                .OrderByDescending(p => p.Variants.Where(v => v.IsActive).Select(v => (long?)v.Price).Min() ?? long.MaxValue)
                .ThenByDescending(p => p.Id),
            "newest" => products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
            _ => products.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        var pagedProducts = products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtoList = pagedProducts.Select(p =>
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
//    // -----------------------------------------------------------------------

    public async Task<Result<ProductDetailDto>> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryAndBrandExistAsync(request.CategoryId, request.BrandId, cancellationToken);

        var product = Product.Create(
            request.CategoryId,
            request.BrandId,
            request.Name,
            request.Description,
            request.SpecificationsJson ?? "{}");

        // SKU phải duy nhất trong toàn hệ thống — kiểm tra trước khi thêm.
        if (request.Variants is not null && request.Variants.Count > 0)
        {
            var skus = request.Variants.Select(v => v.Sku.Trim().ToUpperInvariant()).ToList();
            if (skus.Distinct(StringComparer.Ordinal).Count() != skus.Count)
            {
                throw new ValidationException("variants", "Trong một sản phẩm không được trùng SKU.");
            }

            var existing = await _dbContext.ProductVariants
                .Where(v => skus.Contains(v.Sku))
                .Select(v => v.Sku)
                .ToListAsync(cancellationToken);

            if (existing.Count > 0)
            {
                throw new ConflictException(
                    $"SKU đã tồn tại: {string.Join(", ", existing)}.",
                    "SKU_ALREADY_EXISTS");
            }

            foreach (var variantRequest in request.Variants)
            {
                var variant = ProductVariant.Create(
                    product.Id,
                    variantRequest.Sku,
                    variantRequest.Color,
                    variantRequest.Capacity,
                    variantRequest.Price,
                    variantRequest.StockQuantity);

                product.AddVariant(variant);
            }
        }

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var category = await _dbContext.Categories.FindAsync([product.CategoryId], cancellationToken);
        var brand = await _dbContext.Brands.FindAsync([product.BrandId], cancellationToken);

        return Result.Success(ToDetailDto(product, category?.Name, brand?.Name));
    }

    public async Task<Result<ProductDetailDto>> UpdateProductAsync(long productId, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(productId, includeInactive: true, tracking: true, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm.", "PRODUCT_NOT_FOUND");
        }

        await EnsureCategoryAndBrandExistAsync(request.CategoryId, request.BrandId, cancellationToken);

        product.UpdateInfo(
            request.Name,
            request.Description,
            request.SpecificationsJson ?? "{}",
            request.IsActive);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var category = await _dbContext.Categories.FindAsync([product.CategoryId], cancellationToken);
        var brand = await _dbContext.Brands.FindAsync([product.BrandId], cancellationToken);

        return Result.Success(ToDetailDto(product, category?.Name, brand?.Name));
    }

    public async Task<Result> DeleteProductAsync(long productId, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(productId, includeInactive: true, tracking: true, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm.", "PRODUCT_NOT_FOUND");
        }

        // Sản phẩm đã bán (có phiên bản trong giỏ/đơn) không xoá cứng để giữ
        // tính toàn vẹn dữ liệu lịch sử — ẩn sản phẩm đi.
        product.UpdateInfo(product.Name, product.Description, product.SpecificationsJson, isActive: false);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<ProductVariantDto>> CreateVariantAsync(long productId, CreateVariantRequest request, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(productId, includeInactive: false, cancellationToken);
        if (product == null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm.", "PRODUCT_NOT_FOUND");
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        var skuExists = await _dbContext.ProductVariants.AnyAsync(v => v.Sku == normalizedSku, cancellationToken);
        if (skuExists)
        {
            throw new ConflictException($"SKU \"{normalizedSku}\" đã tồn tại.", "SKU_ALREADY_EXISTS");
        }

        var variant = ProductVariant.Create(
            productId,
            request.Sku,
            request.Color,
            request.Capacity,
            request.Price,
            request.StockQuantity);

        product.AddVariant(variant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToVariantDto(variant));
    }

    public async Task<Result<ProductVariantDto>> UpdateVariantAsync(long variantId, UpdateVariantRequest request, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants.FindAsync([variantId], cancellationToken);
        if (variant is null)
        {
            throw new NotFoundException("Không tìm thấy phiên bản sản phẩm.", "PRODUCT_VARIANT_NOT_FOUND");
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();

        var skuExists = await _dbContext.ProductVariants
            .AnyAsync(v => v.Sku == normalizedSku && v.Id != variantId, cancellationToken);
        if (skuExists)
        {
            throw new ConflictException($"SKU \"{normalizedSku}\" đã tồn tại.", "SKU_ALREADY_EXISTS");
        }

        variant.UpdateInfo(normalizedSku, request.Color, request.Capacity, request.Price, request.IsActive);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToVariantDto(variant));
    }

    public async Task<Result> DeleteVariantAsync(long variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants.FindAsync([variantId], cancellationToken);
        if (variant is null)
        {
            throw new NotFoundException("Không tìm thấy phiên bản sản phẩm.", "PRODUCT_VARIANT_NOT_FOUND");
        }

        // Phiên bản đã bán không xoá cứng — ẩn đi để giữ dữ liệu đơn hàng.
        variant.SetActive(false);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<PagedResult<ProductVariantDto>>> GetVariantsAsync(VariantFilterQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _dbContext.ProductVariants.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Sku))
        {
            var sku = query.Sku.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(v => v.Sku.Contains(sku));
        }

        if (query.ProductId.HasValue)
        {
            baseQuery = baseQuery.Where(v => v.ProductId == query.ProductId.Value);
        }

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(v => v.IsActive == query.IsActive.Value);
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var variants = await baseQuery
            .OrderBy(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = variants.Select(ToVariantDto).ToList();

        return Result.Success(PagedResult<ProductVariantDto>.Create(dtos, page, pageSize, totalCount));
    }

    public async Task<Result<ProductImageDto>> AddProductImageAsync(long productId, CreateProductImageRequest request, CancellationToken cancellationToken = default)
    {
        var product = await LoadProductAsync(productId, includeInactive: false, cancellationToken);
        if (product == null)
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

    /// <summary>
    /// Nạp sản phẩm kèm variants và images.
    /// tracking = true dùng cho các thao tác cập nhật (cần EF theo dõi thay đổi).
    /// </summary>
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
