using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// Áp dụng migration cho toàn bộ DbContext của các module vào một database cho trước.
///
/// Migration được tìm thấy tự động vì chúng nằm cùng assembly với DbContext.
/// </summary>
public static class DatabaseMigrator
{
    public static async Task MigrateAsync(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddDbContext<PhoneStore.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>(
            options => options.UseNpgsql(connectionString,
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));

        services.AddDbContext<PhoneStore.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext>(
            options => options.UseNpgsql(connectionString,
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", "catalog")));

        services.AddDbContext<PhoneStore.Modules.Cart.Infrastructure.Persistence.CartDbContext>(
            options => options.UseNpgsql(connectionString,
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", "cart")));

        services.AddDbContext<PhoneStore.Modules.Order.Infrastructure.Persistence.OrderDbContext>(
            options => options.UseNpgsql(connectionString,
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", "ordering")));

        services.AddDbContext<PhoneStore.Modules.Inventory.Infrastructure.Persistence.InventoryDbContext>(
            options => options.UseNpgsql(connectionString,
                x => x.MigrationsHistoryTable("__EFMigrationsHistory", "inventory")));

        await using var provider = services.BuildServiceProvider();

        await MigrateAsync<PhoneStore.Modules.Identity.Infrastructure.Persistence.IdentityDbContext>(provider);
        await MigrateAsync<PhoneStore.Modules.Catalog.Infrastructure.Persistence.CatalogDbContext>(provider);
        await MigrateAsync<PhoneStore.Modules.Cart.Infrastructure.Persistence.CartDbContext>(provider);
        await MigrateAsync<PhoneStore.Modules.Order.Infrastructure.Persistence.OrderDbContext>(provider);
        await MigrateAsync<PhoneStore.Modules.Inventory.Infrastructure.Persistence.InventoryDbContext>(provider);
    }

    private static async Task MigrateAsync<TContext>(IServiceProvider provider)
        where TContext : DbContext
    {
        var dbContext = provider.GetRequiredService<TContext>();
        await dbContext.Database.MigrateAsync();
    }
}
