using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Modules.Inventory.Domain.Entities;

namespace PhoneStore.Modules.Inventory.Infrastructure.Persistence;

public class InventoryDbContext : DbContext
{
    public const string Schema = "inventory";

    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<ProductStock>(entity =>
        {
            entity.ToTable("product_stocks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.StockQuantity).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.ProductVariantId).IsUnique();
        });

        // Đạt (Backend / DevOps) sẽ cấu hình chi tiết InventoryMovements tại đây (S2-09)
    }
}
