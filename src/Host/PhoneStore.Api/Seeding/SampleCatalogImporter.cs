using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Catalog.Domain.Entities;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;

namespace PhoneStore.Api.Seeding;

/// <summary>
/// Nạp danh mục mẫu đầy đủ từ <c>Seeding/sample-catalog.json</c> (nhúng vào assembly):
/// mỗi sản phẩm có nhiều phiên bản (dung lượng/cấu hình) × màu, kèm giá bán, giá gốc
/// và ảnh theo màu — phục vụ trang chi tiết chọn "Phiên bản" / "Màu sắc".
/// Idempotent: sản phẩm đã có đủ mọi SKU trong file thì bỏ qua (không ghi đè chỉnh sửa
/// của admin); lần đầu gặp, phiên bản cũ trùng màu + dung lượng được cập nhật lại, phiên
/// bản cũ không còn trong file bị ẩn (không xoá để giữ liên kết với đơn hàng cũ).
/// </summary>
internal static class SampleCatalogImporter
{
    private const string ResourceSuffix = "Seeding.sample-catalog.json";
    private const string ImageBase = "/assets/products";

    private sealed record VariantJson(
        string Sku, string Color, string Capacity, long Price, long? OriginalPrice, string? ImageUrl, int Stock);

    private sealed record ProductJson(
        string Slug, string Name, string Category, string Brand, string Description,
        Dictionary<string, string> Specs, List<VariantJson> Variants);

    private sealed record CatalogJson(List<ProductJson> Products);

    public static async Task ImportAsync(CatalogDbContext db, CancellationToken ct)
    {
        var data = Load();
        if (data is null || data.Products.Count == 0)
        {
            return;
        }

        var categories = await db.Categories.ToDictionaryAsync(c => c.Name, ct);
        foreach (var name in data.Products.Select(p => p.Category).Distinct().Where(n => !categories.ContainsKey(n)))
        {
            var category = Category.Create(name);
            db.Categories.Add(category);
            categories[name] = category;
        }

        var brands = await db.Brands.ToDictionaryAsync(b => b.Name, ct);
        foreach (var name in data.Products.Select(p => p.Brand).Distinct().Where(n => !brands.ContainsKey(n)))
        {
            var brand = Brand.Create(name);
            db.Brands.Add(brand);
            brands[name] = brand;
        }

        await db.SaveChangesAsync(ct);

        var products = await db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .ToDictionaryAsync(p => p.Name, ct);

        foreach (var seed in data.Products.Where(p => p.Variants.Count > 0))
        {
            var specsJson = JsonSerializer.Serialize(seed.Specs);

            if (!products.TryGetValue(seed.Name, out var product))
            {
                product = Product.Create(categories[seed.Category].Id, brands[seed.Brand].Id,
                    seed.Name, seed.Description, specsJson);
                db.Products.Add(product);
                await db.SaveChangesAsync(ct); // cần Id để gắn phiên bản và ảnh
                products[seed.Name] = product;
            }
            else
            {
                var existingSkus = product.Variants.Select(v => v.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (seed.Variants.All(v => existingSkus.Contains(v.Sku)))
                {
                    continue; // đã nạp từ lần chạy trước
                }

                if (product.SpecificationsJson is "{}" or "")
                {
                    product.UpdateInfo(product.Name, product.Description, specsJson, product.IsActive);
                }
            }

            if (product.Images.Count == 0)
            {
                db.ProductImages.Add(ProductImage.Create(product.Id, $"{ImageBase}/{seed.Slug}.webp", 0));
            }

            var matched = new HashSet<ProductVariant>();
            foreach (var v in seed.Variants)
            {
                var variant = product.Variants.FirstOrDefault(x => string.Equals(x.Sku, v.Sku, StringComparison.OrdinalIgnoreCase))
                              ?? product.Variants.FirstOrDefault(x => !matched.Contains(x)
                                                                     && x.Color == v.Color && x.Capacity == v.Capacity);
                if (variant is null)
                {
                    variant = ProductVariant.Create(product.Id, v.Sku, v.Color, v.Capacity, v.Price, v.Stock);
                    db.ProductVariants.Add(variant);
                }
                else
                {
                    variant.UpdateInfo(variant.Sku, v.Color, v.Capacity, v.Price, isActive: true);
                }

                variant.SetPresentation(v.ImageUrl, v.OriginalPrice);
                matched.Add(variant);
            }

            foreach (var stale in product.Variants.Where(x => x.IsActive && !matched.Contains(x)))
            {
                stale.SetActive(false);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private static CatalogJson? Load()
    {
        var assembly = typeof(SampleCatalogImporter).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (name is null)
        {
            return null;
        }

        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<CatalogJson>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
