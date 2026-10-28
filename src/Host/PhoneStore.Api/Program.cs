using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;
using PhoneStore.Modules.Cart.Infrastructure.Persistence;
using PhoneStore.Modules.Order.Infrastructure.Persistence;
using PhoneStore.Modules.Inventory.Infrastructure.Persistence;
using PhoneStore.BuildingBlocks;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Infrastructure.Web;
using PhoneStore.BuildingBlocks.Validation;
using PhoneStore.Api.Seeding;
using PhoneStore.Modules.Cart;
using PhoneStore.Modules.Catalog;
using PhoneStore.Modules.Identity;
using PhoneStore.Modules.Inventory;
using PhoneStore.Modules.Order;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Authentication:AllowDevelopmentSigningKey"))
{
    throw new InvalidOperationException("Khóa ký tạm chỉ được bật trong môi trường Development.");
}

// N2LHTBLTT-14: Swagger, Validation và Global Exception Handling.
builder.Services.AddAppSwagger("PhoneStore API", "v1");
builder.Services.AddBuildingBlocksValidation(
    typeof(BuildingBlocksRegistration),
    typeof(IdentityModuleRegistration),
    typeof(CatalogModuleRegistration),
    typeof(CartModuleRegistration),
    typeof(OrderModuleRegistration),
    typeof(InventoryModuleRegistration));

// Register BuildingBlocks (Mediator, Event Bus, Shared Kernel)
// Lưu ý: MediatR phải scan cả các module, nên truyền marker type của từng module vào đây.
builder.Services.AddBuildingBlocks(
    builder.Configuration,
    typeof(BuildingBlocksRegistration),
    typeof(IdentityModuleRegistration),
    typeof(CatalogModuleRegistration),
    typeof(CartModuleRegistration),
    typeof(OrderModuleRegistration),
    typeof(InventoryModuleRegistration));

// Register Modules (Modular Monolith)
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddCartModule(builder.Configuration);
builder.Services.AddOrderModule(builder.Configuration);
builder.Services.AddInventoryModule(builder.Configuration);

// CORS
// Cookie chỉ được chia sẻ với frontend có origin được cấu hình rõ ràng.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowApp", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins);
        }
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

// N2LHTBLTT-40: Seed dữ liệu tối thiểu cho môi trường local (idempotent).
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<CartDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
        await services.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();

        await DatabaseSeeder.SeedAsync(app.Services);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PhoneStore API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseMiddleware<AppExceptionMiddleware>();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/auth")
        || context.Request.Path.StartsWithSegments("/api/v1/me"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
    await next(context);
});
app.UseCors("AllowApp");

// Exception middleware bao cả xác thực vì việc kiểm tra phiên có truy vấn DB.
app.UseAuthentication();
app.UseAuthorization();

// N2LHTBLTT-38: Bảo vệ CSRF cho các endpoint dùng cookie để xác thực.
app.UseMiddleware<CsrfProtectionMiddleware>();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTimeOffset.UtcNow,
    architecture = "Modular Monolith (.NET 10)",
    modules = new[] { "Identity", "Catalog", "Cart", "Order", "Inventory" }
}));

app.MapControllers();

app.Run();

public partial class Program { }
