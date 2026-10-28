using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Modules.Identity.Application.Services;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.Modules.Identity;

public static class IdentityModuleRegistration
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Port=5433;Database=phonestore_db;Username=postgres;Password=postgrespassword";

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)));

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IIdentityService, IdentityService>();

        return services;
    }
}
