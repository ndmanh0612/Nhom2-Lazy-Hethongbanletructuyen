using PhoneStore.BuildingBlocks;
using PhoneStore.Modules.Cart;
using PhoneStore.Modules.Catalog;
using PhoneStore.Modules.Identity;
using PhoneStore.Modules.Inventory;
using PhoneStore.Modules.Order;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register BuildingBlocks (Mediator, Event Bus, Shared Kernel)
builder.Services.AddBuildingBlocks(typeof(Program));

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

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var inventoryDb = scope.ServiceProvider.GetRequiredService<PhoneStore.Modules.Inventory.Infrastructure.Persistence.InventoryDbContext>();
        inventoryDb.Database.Migrate();
    }

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PhoneStore API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");

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
