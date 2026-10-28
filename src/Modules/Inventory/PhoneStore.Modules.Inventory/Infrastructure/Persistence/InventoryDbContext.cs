using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Inventory.Domain.Entities;

namespace PhoneStore.Modules.Inventory.Infrastructure.Persistence;

public class InventoryDbContext : DbContext
{
    public const string Schema = "inventory";

    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        // ── Stock (Aggregate Root: tồn kho theo SKU) ──
        modelBuilder.Entity<Stock>(entity =>
        {
            entity.ToTable("stocks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Sku).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.Sku).IsUnique();
            entity.HasIndex(e => e.ProductVariantId).IsUnique();
            entity.Property(e => e.QuantityOnHand).HasDefaultValue(0);
            entity.Property(e => e.QuantityReserved).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasMany(e => e.Movements)
                .WithOne()
                .HasForeignKey(m => m.StockId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── StockMovement (lịch sử biến động kho) ──
        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.QuantityAfter).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.StockId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
