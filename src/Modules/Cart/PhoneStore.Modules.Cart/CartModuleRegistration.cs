using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Modules.Cart.Infrastructure.Persistence;

namespace PhoneStore.Modules.Cart;

public static class CartModuleRegistration
{
    public static IServiceCollection AddCartModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres";

        services.AddDbContext<CartDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", CartDbContext.Schema)));

        return services;
    }
}

[ApiController]
[Route("api/v1/cart")]
public class CartController : ControllerBase
{
    // Task của Toàn: S2-08 & S3-05
    [HttpGet]
    public IActionResult GetCart() => Ok(new { message = "Cart Module Ready - Assigned to Toàn" });
}
