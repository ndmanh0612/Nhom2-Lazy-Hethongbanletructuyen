using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Catalog.Domain.Entities;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;
using PhoneStore.Modules.Inventory.Domain.Entities;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;

namespace PhoneStore.Api.Seeding;

/// <summary>
/// N2LHTBLTT-40: Seed dữ liệu tối thiểu để chạy thử end-to-end local.
/// Idempotent — chạy lại nhiều lần không tạo trùng dữ liệu.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        await SeedIdentityAsync(provider, cancellationToken);
        await SeedCatalogAsync(provider, cancellationToken);
        await SeedInventoryAsync(provider, cancellationToken);
    }

    private static async Task SeedIdentityAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var dbContext = provider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = provider.GetRequiredService<IPasswordHasher>();

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.Users.AddRange(
            User.CreateCustomer("customer@phonestore.vn", passwordHasher.HashPassword("Customer@123"), "Nguyễn Văn Khách", "0912345678"),
            User.CreateStaff("staff@phonestore.vn", passwordHasher.HashPassword("Staff@123"), "Trần Nhân Viên", "0912345679"),
            User.CreateCustomer("demo@phonestore.vn", passwordHasher.HashPassword("Demo@123"), "Tài Khoản Demo", "0900000000"));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCatalogAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var dbContext = provider.GetRequiredService<CatalogDbContext>();

        if (await dbContext.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var phoneCategory = Category.Create("Điện thoại");
        var laptopCategory = Category.Create("Laptop");
        var brandApple = Brand.Create("Apple");
        var brandSamsung = Brand.Create("Samsung");
        var brandDell = Brand.Create("Dell");

        dbContext.Categories.AddRange(phoneCategory, laptopCategory);
        dbContext.Brands.AddRange(brandApple, brandSamsung, brandDell);
        await dbContext.SaveChangesAsync(cancellationToken);

        var iphone15 = Product.Create(phoneCategory.Id, brandApple.Id, "iPhone 15", "Smartphone flagship của Apple.");
        var galaxyS24 = Product.Create(phoneCategory.Id, brandSamsung.Id, "Galaxy S24", "Smartphone cao cấp của Samsung.");
        var dellXps = Product.Create(laptopCategory.Id, brandDell.Id, "Dell XPS 15", "Laptop cao cấp cho dân văn phòng.");

        dbContext.Products.AddRange(iphone15, galaxyS24, dellXps);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.ProductVariants.AddRange(
            ProductVariant.Create(iphone15.Id, "IP15-128-BLK", "Đen", "128GB", 22_900_000, 50),
            ProductVariant.Create(iphone15.Id, "IP15-256-BLU", "Xanh dương", "256GB", 25_900_000, 30),
            ProductVariant.Create(galaxyS24.Id, "GS24-256-GRY", "Xám", "256GB", 23_500_000, 40),
            ProductVariant.Create(dellXps.Id, "XPS15-512-SLV", "Bạc", "512GB", 42_900_000, 15));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedInventoryAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        var dbContext = provider.GetRequiredService<InventoryDbContext>();
        var catalogDbContext = provider.GetRequiredService<CatalogDbContext>();

        var variantIds = await catalogDbContext.ProductVariants
            .Select(variant => variant.Id)
            .ToListAsync(cancellationToken);

        var existingVariantIds = await dbContext.ProductStocks
            .Select(stock => stock.ProductVariantId)
            .ToListAsync(cancellationToken);

        var missing = variantIds
            .Where(id => !existingVariantIds.Contains(id))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.ProductStocks.AddRange(missing.Select(id => ProductStock.Create(id, 100)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
