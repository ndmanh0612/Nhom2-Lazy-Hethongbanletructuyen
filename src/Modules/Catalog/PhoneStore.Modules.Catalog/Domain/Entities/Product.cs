using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Catalog.Domain.Entities;

public class Product : AggregateRoot<long>
{
    public long CategoryId { get; private set; }
    public long BrandId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string SpecificationsJson { get; private set; } = "{}";
    public bool IsActive { get; private set; } = true;

    private readonly List<ProductVariant> _variants = [];
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    private readonly List<ProductImage> _images = [];
    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    protected Product() { }

    public static Product Create(long categoryId, long brandId, string name, string description, string specificationsJson = "{}")
    {
        return new Product
        {
            CategoryId = categoryId,
            BrandId = brandId,
            Name = name.Trim(),
            Description = description.Trim(),
            SpecificationsJson = string.IsNullOrWhiteSpace(specificationsJson) ? "{}" : specificationsJson,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void AddVariant(ProductVariant variant)
    {
        _variants.Add(variant);
    }

    public void AddImage(ProductImage image)
    {
        _images.Add(image);
    }

    public void UpdateInfo(string name, string description, string specificationsJson, bool isActive)
    {
        Name = name.Trim();
        Description = description.Trim();
        SpecificationsJson = specificationsJson;
        IsActive = isActive;
        Touch();
    }
}

public class ProductVariant : BaseEntity<long>
{
    public long ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;
    public string Capacity { get; private set; } = string.Empty;
    public long Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; } = true;

    protected ProductVariant() { }

    public static ProductVariant Create(long productId, string sku, string color, string capacity, long price, int stockQuantity = 0)
    {
        if (price <= 0)
            throw new ArgumentException("Giá phiên bản sản phẩm phải lớn hơn 0.", nameof(price));

        if (stockQuantity < 0)
            throw new ArgumentException("Số lượng tồn kho không được âm.", nameof(stockQuantity));

        return new ProductVariant
        {
            ProductId = productId,
            Sku = sku.Trim().ToUpperInvariant(),
            Color = color.Trim(),
            Capacity = capacity.Trim(),
            Price = price,
            StockQuantity = stockQuantity,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdatePrice(long newPrice)
    {
        if (newPrice <= 0)
            throw new ArgumentException("Giá phải lớn hơn 0.", nameof(newPrice));

        Price = newPrice;
        Touch();
    }

    public void AdjustStock(int delta)
    {
        var newStock = StockQuantity + delta;
        if (newStock < 0)
            throw new InvalidOperationException($"Tồn kho không đủ. Hiện có {StockQuantity}, yêu cầu giảm {-delta}.");

        StockQuantity = newStock;
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }
}

public class ProductImage : BaseEntity<long>
{
    public long ProductId { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    protected ProductImage() { }

    public static ProductImage Create(long productId, string imageUrl, int sortOrder = 0)
    {
        return new ProductImage
        {
            ProductId = productId,
            ImageUrl = imageUrl.Trim(),
            SortOrder = sortOrder < 0 ? 0 : sortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
