using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Modules.Catalog.Application.Services;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;

namespace PhoneStore.Modules.Catalog;

public static class CatalogModuleRegistration
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres";

        services.AddDbContext<CatalogDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbContext.Schema)));

        services.AddScoped<ICatalogService, CatalogService>();

        return services;
    }
}
