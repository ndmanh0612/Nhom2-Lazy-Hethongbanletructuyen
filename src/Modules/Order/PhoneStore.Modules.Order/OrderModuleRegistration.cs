using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Modules.Order.Infrastructure.Persistence;

namespace PhoneStore.Modules.Order;

public static class OrderModuleRegistration
{
    public static IServiceCollection AddOrderModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres";

        services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", OrderDbContext.Schema)));

        return services;
    }
}

[ApiController]
[Route("api/v1/orders")]
public class OrdersController : ControllerBase
{
    // Task của Toàn & Mạnh: S2-08 & S3-06
    [HttpGet]
    public IActionResult GetOrders() => Ok(new { message = "Order Module Ready - Assigned to Toàn & Mạnh" });
}
