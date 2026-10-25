using PhoneStore.BuildingBlocks;
using PhoneStore.BuildingBlocks.Infrastructure.Web;
using PhoneStore.BuildingBlocks.Validation;
using PhoneStore.Api.Seeding;
using PhoneStore.Modules.Cart;
using PhoneStore.Modules.Catalog;
using PhoneStore.Modules.Identity;
using PhoneStore.Modules.Inventory;
using PhoneStore.Modules.Order;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// N2LHTBLTT-40: Seed dữ liệu tối thiểu cho môi trường local (idempotent).
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
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

app.UseCors("AllowAll");

app.UseMiddleware<AppExceptionMiddleware>();

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
