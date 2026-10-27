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
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

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

        // N2LHTBLTT-21 (FR-14): Nhật ký tồn kho.
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.QuantityBefore).IsRequired();
            entity.Property(e => e.QuantityAfter).IsRequired();
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.ProductVariantId);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => new { e.Type, e.CreatedAt });
        });
    }
}
