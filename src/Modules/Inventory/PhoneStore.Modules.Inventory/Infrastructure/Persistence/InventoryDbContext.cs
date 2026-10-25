using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneStore.Modules.Inventory.Infrastructure.Persistence;

public class InventoryDbContext : DbContext
{
    public const string Schema = "inventory";

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        // Đạt (Backend / DevOps) sẽ cấu hình chi tiết InventoryMovements tại đây (S2-09)
    }
}
