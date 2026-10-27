using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhoneStore.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Cho phép `dotnet ef migrations add` chạy được cho module Identity mà không
/// cần khởi động toàn bộ Web API host.
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=phonestore_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema))
            .Options;

        return new IdentityDbContext(optionsBuilder);
    }
}
