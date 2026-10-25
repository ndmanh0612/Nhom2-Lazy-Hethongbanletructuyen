using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneStore.Modules.Order.Infrastructure.Persistence;

public class OrderDbContext : DbContext
{
    public const string Schema = "ordering";

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        // Mạnh & Toàn sẽ cấu hình chi tiết Orders, OrderItems, OrderStatusHistories tại đây
    }
}
