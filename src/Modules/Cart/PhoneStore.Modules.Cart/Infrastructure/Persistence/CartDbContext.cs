using Microsoft.EntityFrameworkCore;

namespace PhoneStore.Modules.Cart.Infrastructure.Persistence;

public class CartDbContext : DbContext
{
    public const string Schema = "cart";

    public CartDbContext(DbContextOptions<CartDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        // Toàn (Backend Dev) sẽ cấu hình chi tiết Cart và CartItem tại đây
    }
}
