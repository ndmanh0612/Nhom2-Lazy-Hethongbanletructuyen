using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhoneStore.Modules.Inventory.Infrastructure.Persistence;

/// <summary>
/// Cho phép `dotnet ef migrations add` chạy được cho module Inventory mà không
/// cần khởi động toàn bộ Web API host.
/// </summary>
public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=phonestore_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema))
            .Options;

        return new InventoryDbContext(optionsBuilder);
    }
}
