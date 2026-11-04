using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Catalog.Domain.Entities;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;
using PhoneStore.Modules.Inventory.Domain.Entities;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;

namespace PhoneStore.Api.Seeding;

/// <summary>
/// Dữ liệu mẫu phong phú cho môi trường demo/staging: nhiều danh mục, hãng, sản phẩm,
/// ảnh minh họa và tồn kho. Chạy khi bật cờ <c>Database:AutoSeedSample</c> (mặc định tắt
/// để không ảnh hưởng bộ test dùng <see cref="DatabaseSeeder"/>).
/// Idempotent: nhận diện theo tên danh mục/hãng/sản phẩm và SKU, chạy lại không tạo trùng.
/// </summary>
public static class SampleDataSeeder
{
    private const string ImageBase = "/assets/products";

    /// <summary>Mức tồn mặc định của <see cref="DatabaseSeeder"/> — coi là "chưa chỉnh tay".</summary>
    private const int BaseSeedStock = 10_000;

    private sealed record VariantSeed(string Sku, string Color, string Capacity, long Price, int Stock);

    private sealed record ProductSeed(
        string Slug, string Category, string Brand, string Name, string Description,
        string SpecsJson, VariantSeed[] Variants)
    {
        /// <summary>Ảnh mặt sau chỉ có cho điện thoại, máy tính bảng, laptop.</summary>
        public bool HasBackImage => Category is "Điện thoại" or "Máy tính bảng" or "Laptop";
    }

    private static readonly string[] Categories = ["Điện thoại", "Laptop", "Máy tính bảng", "Phụ kiện"];

    private static readonly string[] Brands =
        ["Apple", "Samsung", "Dell", "Xiaomi", "OPPO", "vivo", "realme", "Honor", "ASUS", "Lenovo", "HP", "Anker"];

    private static readonly ProductSeed[] Products =
    [
        // --- Sản phẩm đã có trong seed gốc: chỉ bổ sung ảnh/thông số ---
        new("iphone-15", "Điện thoại", "Apple", "iPhone 15", "Smartphone flagship của Apple.",
            """{"Màn hình":"6.1 inch Super Retina XDR","Chip":"Apple A16 Bionic","Camera sau":"48MP + 12MP","Pin":"3349 mAh","Hệ điều hành":"iOS 17"}""", []),
        new("galaxy-s24", "Điện thoại", "Samsung", "Galaxy S24", "Smartphone cao cấp của Samsung.",
            """{"Màn hình":"6.2 inch Dynamic AMOLED 2X 120Hz","Chip":"Exynos 2400","Camera sau":"50MP + 12MP + 10MP","Pin":"4000 mAh","Hệ điều hành":"Android 14"}""", []),
        new("dell-xps-15", "Laptop", "Dell", "Dell XPS 15", "Laptop cao cấp cho dân văn phòng.",
            """{"Màn hình":"15.6 inch 3.5K OLED","CPU":"Intel Core i7-13700H","RAM":"16GB DDR5","Ổ cứng":"SSD 512GB","Card đồ họa":"NVIDIA RTX 4050"}""", []),

        // --- Điện thoại ---
        new("iphone-15-pro-max", "Điện thoại", "Apple", "iPhone 15 Pro Max",
            "Khung titan cao cấp, chip A17 Pro, camera tele 5x, cổng USB-C.",
            """{"Màn hình":"6.7 inch Super Retina XDR 120Hz","Chip":"Apple A17 Pro","Camera sau":"48MP + 12MP tele 5x + 12MP","Pin":"4422 mAh","Hệ điều hành":"iOS 17"}""",
            [new("IP15PM-256-TIT", "Titan tự nhiên", "256GB", 29_990_000, 25), new("IP15PM-512-TIT", "Titan tự nhiên", "512GB", 36_990_000, 12)]),
        new("galaxy-s24-ultra", "Điện thoại", "Samsung", "Galaxy S24 Ultra",
            "Bút S Pen, camera 200MP, khung titan và Galaxy AI.",
            """{"Màn hình":"6.8 inch Dynamic AMOLED 2X 120Hz","Chip":"Snapdragon 8 Gen 3 for Galaxy","Camera sau":"200MP + 50MP + 12MP + 10MP","Pin":"5000 mAh","Hệ điều hành":"Android 14"}""",
            [new("GS24U-256-BLK", "Đen titan", "256GB", 29_990_000, 22), new("GS24U-512-GRY", "Xám titan", "512GB", 33_990_000, 14)]),
        new("galaxy-a55", "Điện thoại", "Samsung", "Galaxy A55 5G",
            "Tầm trung khung kim loại, màn Super AMOLED 120Hz, kháng nước IP67.",
            """{"Màn hình":"6.6 inch Super AMOLED 120Hz","Chip":"Exynos 1480","Camera sau":"50MP + 12MP + 5MP","Pin":"5000 mAh","Hệ điều hành":"Android 14"}""",
            [new("A55-256-NVY", "Xanh navy", "8GB/256GB", 9_990_000, 60), new("A55-128-LIL", "Tím lilac", "8GB/128GB", 8_990_000, 45)]),
        new("xiaomi-14-ultra", "Điện thoại", "Xiaomi", "Xiaomi 14 Ultra",
            "Camera Leica 1 inch, quay video 8K, sạc nhanh 90W.",
            """{"Màn hình":"6.73 inch LTPO AMOLED 120Hz","Chip":"Snapdragon 8 Gen 3","Camera sau":"4 x 50MP Leica","Pin":"5000 mAh","Hệ điều hành":"HyperOS"}""",
            [new("XM14U-512-BLK", "Đen", "16GB/512GB", 28_990_000, 6)]),
        new("redmi-note-13-pro", "Điện thoại", "Xiaomi", "Redmi Note 13 Pro",
            "Camera 200MP, sạc nhanh 67W, màn AMOLED 120Hz giá tốt.",
            """{"Màn hình":"6.67 inch AMOLED 120Hz","Chip":"Snapdragon 7s Gen 2","Camera sau":"200MP + 8MP + 2MP","Pin":"5100 mAh","Hệ điều hành":"HyperOS"}""",
            [new("RN13P-256-BLK", "Đen", "8GB/256GB", 7_290_000, 80), new("RN13P-256-BLU", "Xanh", "8GB/256GB", 7_290_000, 55)]),
        new("oppo-find-x7", "Điện thoại", "OPPO", "OPPO Find X7",
            "Camera Hasselblad, chip Dimensity 9300, sạc nhanh 100W.",
            """{"Màn hình":"6.78 inch LTPO AMOLED 120Hz","Chip":"Dimensity 9300","Camera sau":"50MP + 50MP + 64MP","Pin":"5000 mAh","Hệ điều hành":"ColorOS 14"}""",
            [new("FX7-256-BLK", "Đen", "12GB/256GB", 15_990_000, 18)]),
        new("oppo-reno11-f", "Điện thoại", "OPPO", "OPPO Reno11 F 5G",
            "Thiết kế mỏng nhẹ, chụp chân dung đẹp, sạc nhanh 67W.",
            """{"Màn hình":"6.7 inch AMOLED 120Hz","Chip":"Dimensity 7050","Camera sau":"64MP + 8MP + 2MP","Pin":"5000 mAh","Hệ điều hành":"ColorOS 14"}""",
            [new("RN11F-256-GRN", "Xanh lá", "8GB/256GB", 8_490_000, 40)]),
        new("vivo-v30-e", "Điện thoại", "vivo", "vivo V30e",
            "Camera Aura Light chân dung, màn cong AMOLED, pin 5500 mAh.",
            """{"Màn hình":"6.78 inch AMOLED 120Hz","Chip":"Snapdragon 6 Gen 1","Camera sau":"50MP + 8MP","Pin":"5500 mAh","Hệ điều hành":"Funtouch OS 14"}""",
            [new("V30E-256-PRP", "Tím", "8GB/256GB", 9_290_000, 35)]),
        new("realme-12-pro", "Điện thoại", "realme", "realme 12 Pro 5G",
            "Thiết kế đồng hồ sang trọng, camera tele chân dung 2x.",
            """{"Màn hình":"6.7 inch AMOLED 120Hz","Chip":"Snapdragon 6 Gen 1","Camera sau":"50MP + 32MP tele + 8MP","Pin":"5000 mAh","Hệ điều hành":"realme UI 5.0"}""",
            [new("R12P-256-GLD", "Vàng be", "8GB/256GB", 9_490_000, 28)]),
        new("honor-magic6-lite", "Điện thoại", "Honor", "Honor Magic6 Lite",
            "Màn hình cong 1.5K chống va đập, pin silicon-carbon 5300 mAh.",
            """{"Màn hình":"6.78 inch AMOLED cong 120Hz","Chip":"Snapdragon 6 Gen 1","Camera sau":"108MP + 5MP","Pin":"5300 mAh","Hệ điều hành":"MagicOS 7.2"}""",
            [new("HM6L-256-GRN", "Xanh lục", "8GB/256GB", 8_290_000, 30)]),

        // --- Máy tính bảng ---
        new("ipad-air-m2-11", "Máy tính bảng", "Apple", "iPad Air M2 11 inch",
            "Chip M2 mạnh mẽ, màn Liquid Retina, hỗ trợ Apple Pencil Pro.",
            """{"Màn hình":"11 inch Liquid Retina","Chip":"Apple M2","Camera sau":"12MP","Kết nối":"Wi-Fi 6E","Hệ điều hành":"iPadOS 17"}""",
            [new("IPAD-AIR-128-BLU", "Xanh dương", "128GB", 14_990_000, 20), new("IPAD-AIR-256-BLU", "Xanh dương", "256GB", 17_990_000, 12)]),
        new("galaxy-tab-s9-fe", "Máy tính bảng", "Samsung", "Galaxy Tab S9 FE",
            "Kháng nước IP68, đi kèm bút S Pen, màn hình 10.9 inch 90Hz.",
            """{"Màn hình":"10.9 inch TFT 90Hz","Chip":"Exynos 1380","Camera sau":"8MP","Pin":"8000 mAh","Hệ điều hành":"Android 13"}""",
            [new("TABS9FE-128-GRY", "Xám", "6GB/128GB", 9_490_000, 25)]),

        // --- Laptop ---
        new("macbook-air-m2-13", "Laptop", "Apple", "MacBook Air M2 13 inch",
            "Mỏng nhẹ 1.24kg, chip M2, pin đến 18 giờ, màn Liquid Retina.",
            """{"Màn hình":"13.6 inch Liquid Retina","Chip":"Apple M2 8 nhân CPU","RAM":"8GB","Pin":"Đến 18 giờ","Hệ điều hành":"macOS"}""",
            [new("MBA-M2-256-MID", "Midnight", "8GB/256GB", 22_990_000, 15), new("MBA-M2-512-MID", "Midnight", "8GB/512GB", 27_990_000, 5)]),
        new("asus-vivobook-15", "Laptop", "ASUS", "ASUS Vivobook 15",
            "Laptop học tập văn phòng, màn 15.6 inch FHD, bàn phím số đầy đủ.",
            """{"Màn hình":"15.6 inch FHD IPS","CPU":"Intel Core i5-1235U","RAM":"16GB DDR4","Ổ cứng":"SSD 512GB","Trọng lượng":"1.7kg"}""",
            [new("VB15-512-SLV", "Bạc", "16GB/512GB", 13_490_000, 30)]),
        new("lenovo-thinkpad-e14", "Laptop", "Lenovo", "Lenovo ThinkPad E14 Gen 5",
            "Bền bỉ chuẩn quân đội, bàn phím ThinkPad nổi tiếng, bảo mật vân tay.",
            """{"Màn hình":"14 inch WUXGA IPS","CPU":"Intel Core i5-1335U","RAM":"16GB DDR4","Ổ cứng":"SSD 512GB","Trọng lượng":"1.41kg"}""",
            [new("TPE14-512-BLK", "Đen", "16GB/512GB", 16_990_000, 12)]),
        new("hp-pavilion-15", "Laptop", "HP", "HP Pavilion 15",
            "Thiết kế kim loại, loa Bang & Olufsen, hiệu năng ổn định.",
            """{"Màn hình":"15.6 inch FHD IPS","CPU":"Intel Core i5-1335U","RAM":"16GB DDR4","Ổ cứng":"SSD 512GB","Trọng lượng":"1.75kg"}""",
            [new("PAV15-512-SLV", "Bạc", "16GB/512GB", 14_490_000, 18)]),

        // --- Phụ kiện ---
        new("airpods-pro-2", "Phụ kiện", "Apple", "AirPods Pro 2 (USB-C)",
            "Chống ồn chủ động gấp 2 lần, chip H2, hộp sạc USB-C.",
            """{"Chống ồn":"Chủ động (ANC)","Chip":"Apple H2","Pin":"6 giờ (30 giờ cùng hộp)","Kết nối":"Bluetooth 5.3","Sạc":"USB-C, MagSafe"}""",
            [new("APP2-USBC-WHT", "Trắng", "USB-C", 5_790_000, 50)]),
        new("galaxy-buds2-pro", "Phụ kiện", "Samsung", "Galaxy Buds2 Pro",
            "Âm thanh Hi-Fi 24bit, chống ồn thông minh, thiết kế nhỏ gọn.",
            """{"Chống ồn":"Chủ động (ANC)","Driver":"2 chiều 10mm + 5.3mm","Pin":"5 giờ (18 giờ cùng hộp)","Kết nối":"Bluetooth 5.3","Chống nước":"IPX7"}""",
            [new("BUDS2P-GRY", "Xám than", "Tiêu chuẩn", 3_990_000, 40)]),
        new("anker-powerport-65w", "Phụ kiện", "Anker", "Sạc nhanh Anker 65W 2 cổng",
            "Sạc GaN nhỏ gọn, sạc cùng lúc laptop và điện thoại.",
            """{"Công suất":"65W","Cổng ra":"2 x USB-C","Công nghệ":"GaN, Power Delivery 3.0","Bảo vệ":"Quá nhiệt, quá dòng"}""",
            [new("ANK65-BLK", "Đen", "65W", 790_000, 120)]),
        new("anker-powercore-20000", "Phụ kiện", "Anker", "Pin dự phòng Anker 20.000mAh 22.5W",
            "Dung lượng lớn, sạc nhanh 22.5W, đủ sạc iPhone 15 khoảng 4 lần.",
            """{"Dung lượng":"20.000 mAh","Công suất":"22.5W","Cổng":"2 x USB-A, 1 x USB-C","Trọng lượng":"350g"}""",
            [new("ANK20K-BLK", "Đen", "20.000mAh", 1_090_000, 90)]),
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await SeedCatalogAsync(catalog, cancellationToken);
        await SyncInventoryAsync(catalog, inventory, cancellationToken);
    }

    private static async Task SeedCatalogAsync(CatalogDbContext db, CancellationToken ct)
    {
        var categories = await db.Categories.ToDictionaryAsync(c => c.Name, ct);
        foreach (var name in Categories.Where(n => !categories.ContainsKey(n)))
        {
            var category = Category.Create(name);
            db.Categories.Add(category);
            categories[name] = category;
        }

        var brands = await db.Brands.ToDictionaryAsync(b => b.Name, ct);
        foreach (var name in Brands.Where(n => !brands.ContainsKey(n)))
        {
            var brand = Brand.Create(name);
            db.Brands.Add(brand);
            brands[name] = brand;
        }

        await db.SaveChangesAsync(ct);

        var existingProducts = await db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .ToDictionaryAsync(p => p.Name, ct);

        foreach (var seed in Products)
        {
            if (!existingProducts.TryGetValue(seed.Name, out var product))
            {
                product = Product.Create(categories[seed.Category].Id, brands[seed.Brand].Id,
                    seed.Name, seed.Description, seed.SpecsJson);
                db.Products.Add(product);
                await db.SaveChangesAsync(ct); // cần Id để gắn phiên bản và ảnh
                existingProducts[seed.Name] = product;
            }
            else if (product.SpecificationsJson is "{}" or "")
            {
                product.UpdateInfo(product.Name, product.Description, seed.SpecsJson, product.IsActive);
            }

            var knownSkus = product.Variants.Select(v => v.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var v in seed.Variants.Where(v => !knownSkus.Contains(v.Sku)))
            {
                db.ProductVariants.Add(ProductVariant.Create(product.Id, v.Sku, v.Color, v.Capacity, v.Price, v.Stock));
            }

            if (product.Images.Count == 0)
            {
                db.ProductImages.Add(ProductImage.Create(product.Id, $"{ImageBase}/{seed.Slug}.svg", 0));
                if (seed.HasBackImage)
                {
                    db.ProductImages.Add(ProductImage.Create(product.Id, $"{ImageBase}/{seed.Slug}-2.svg", 1));
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Làm tồn kho khớp nhau. Inventory là nguồn sự thật (nơi trừ kho khi đặt hàng):
    /// biến thể chưa có dòng tồn được tạo theo số lượng của catalog; dòng còn đúng mức
    /// mặc định 10.000 của seed gốc được hạ về số lượng catalog; sau cùng catalog được
    /// đồng bộ theo inventory để số hiển thị trên storefront luôn đúng.
    /// </summary>
    private static async Task SyncInventoryAsync(CatalogDbContext catalog, InventoryDbContext inventory, CancellationToken ct)
    {
        var variants = await catalog.ProductVariants.ToListAsync(ct);
        var stocks = await inventory.ProductStocks.ToDictionaryAsync(s => s.ProductVariantId, ct);
        var touched = await inventory.StockMovements.Select(m => m.ProductVariantId).Distinct().ToListAsync(ct);
        var touchedSet = touched.ToHashSet();

        foreach (var variant in variants)
        {
            if (!stocks.TryGetValue(variant.Id, out var stock))
            {
                stock = ProductStock.Create(variant.Id, variant.StockQuantity);
                inventory.ProductStocks.Add(stock);
                stocks[variant.Id] = stock;
            }
            else if (stock.StockQuantity == BaseSeedStock && !touchedSet.Contains(variant.Id) && variant.StockQuantity != BaseSeedStock)
            {
                stock.Adjust(variant.StockQuantity - BaseSeedStock);
            }
        }

        await inventory.SaveChangesAsync(ct);

        foreach (var variant in variants)
        {
            var delta = stocks[variant.Id].StockQuantity - variant.StockQuantity;
            if (delta != 0)
            {
                variant.AdjustStock(delta);
            }
        }

        await catalog.SaveChangesAsync(ct);
    }
}
