using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Cart.Domain.Entities;
using PhoneStore.Modules.Cart.Domain.Enums;
using CartEntity = PhoneStore.Modules.Cart.Domain.Entities.Cart;

namespace PhoneStore.Modules.Cart.Infrastructure.Persistence;

public class CartDbContext : DbContext
{
    public const string Schema = "cart";

    public DbSet<CartEntity> Carts => Set<CartEntity>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    public CartDbContext(DbContextOptions<CartDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<CartEntity>(entity =>
        {
            entity.ToTable("carts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.Status });

            entity.HasMany(e => e.Items)
                .WithOne()
                .HasForeignKey(item => item.CartId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("cart_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ProductName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Sku).HasMaxLength(64).IsRequired();
            entity.Property(e => e.UnitPrice).IsRequired();
            entity.Property(e => e.Quantity).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()").IsRequired();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()").IsRequired();

            entity.HasIndex(e => e.CartId);
            entity.HasIndex(e => new { e.CartId, e.ProductVariantId }).IsUnique();
        });
    }
}
