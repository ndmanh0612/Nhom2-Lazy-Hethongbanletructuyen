using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Order.Domain.Entities;
using OrderEntity = PhoneStore.Modules.Order.Domain.Entities.Order;

namespace PhoneStore.Modules.Order.Infrastructure.Persistence;

public class OrderDbContext : DbContext
{
    public const string Schema = "ordering";

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.OrderNumber).HasMaxLength(32).IsRequired();
            entity.Property(e => e.CartId);
            entity.Property(e => e.Carrier).HasMaxLength(100);
            entity.Property(e => e.TrackingNumber).HasMaxLength(64);
            entity.Property(e => e.CollectedAmount);
            entity.Property(e => e.CollectedAt);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.PaymentMethod).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ReceiverName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ReceiverPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ShippingAddress).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.CancelReason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.CartId);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });

            entity.HasMany(e => e.Items)
                .WithOne(item => item.Order)
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.StatusHistories)
                .WithOne(history => history.Order)
                .HasForeignKey(history => history.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Sku).HasMaxLength(64).IsRequired();
            entity.Property(e => e.UnitPrice).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.OrderId);
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.ToTable("order_status_histories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.ChangedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.OrderId);
        });
    }
}
