using Microsoft.AspNetCore.HttpOverrides;
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

// N2LHTBLTT-41: Hỗ trợ chạy sau reverse proxy (nginx / traefik / cloud load balancer).
// ForwardedHeaders phải được đăng ký trước khi pipeline dựng, nếu không
// scheme/host gốc sẽ bị sai và cookie Secure + CORS origin sẽ không khớp.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// N2LHTBLTT-41: Áp dụng header do proxy chuyển tiếp (X-Forwarded-Proto/Host).
app.UseForwardedHeaders();

// N2LHTBLTT-40: Seed dữ liệu tối thiểu cho môi trường local (idempotent).
// N2LHTBLTT-41: Môi trường Production dùng cờ cấu hình để bật/tắt migration và seed,
// vì migration cần chạy khi DB đã sẵn sàng (thường là job riêng trước khi restart API).
var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", app.Environment.IsDevelopment());
var autoSeed = app.Configuration.GetValue("Database:AutoSeed", app.Environment.IsDevelopment());
// Dữ liệu mẫu phong phú (nhiều sản phẩm, ảnh, tồn kho) cho demo — mặc định tắt để test không bị ảnh hưởng.
var autoSeedSample = app.Configuration.GetValue("Database:AutoSeedSample", false);

if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    await services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<CartDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
}

if (autoSeed)
{
    await DatabaseSeeder.SeedAsync(app.Services);
}

if (autoSeedSample)
{
    await SampleDataSeeder.SeedAsync(app.Services);
}

// N2LHTBLTT-41: Swagger bật theo cấu hình để dùng được trên môi trường demo/staging.
// Ở Development Swagger chiếm root ("/") cho tiện; ở Production phải để prefix
// "swagger" vì root dành cho storefront SPA.
var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());
if (swaggerEnabled)
{
    var swaggerRoutePrefix = app.Environment.IsDevelopment() ? string.Empty : "swagger";

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // UseSwagger phục vụ OpenAPI JSON tại /swagger/v1/swagger.json.
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PhoneStore API v1");
        c.RoutePrefix = swaggerRoutePrefix;
    });
}

// N2LHTBLTT-41: Phục vụ SPA storefront (wwwroot) — chạy trước khi routing API.
app.UseDefaultFiles();
app.UseStaticFiles();

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

// N2LHTBLTT-41: Fallback SPA — mọi đường dẫn không phải API/static đều trả index.html
// để frontend dùng History API routing hoạt động khi người dùng F5 hoặc deep-link.
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indexFile = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
    if (!File.Exists(indexFile))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexFile);
});

app.Run();

public partial class Program { }
