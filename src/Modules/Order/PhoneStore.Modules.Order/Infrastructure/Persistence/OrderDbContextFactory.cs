using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhoneStore.Modules.Order.Infrastructure.Persistence;

/// <summary>
/// Cho phép `dotnet ef migrations add` chạy được cho module Order mà không
/// cần khởi động toàn bộ Web API host.
/// </summary>
public class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=phonestore_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", OrderDbContext.Schema))
            .Options;

        return new OrderDbContext(optionsBuilder);
    }
}
