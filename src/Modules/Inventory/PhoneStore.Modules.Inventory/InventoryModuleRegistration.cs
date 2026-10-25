using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;

namespace PhoneStore.Modules.Inventory;

public static class InventoryModuleRegistration
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres";

        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema)));

        return services;
    }
}

[ApiController]
[Route("api/v1/admin/inventory")]
public class InventoryController : ControllerBase
{
    // Task của Đạt: S2-09 & S3-07
    [HttpGet("movements")]
    public IActionResult GetMovements() => Ok(new { message = "Inventory Module Ready - Assigned to Đạt" });
}
