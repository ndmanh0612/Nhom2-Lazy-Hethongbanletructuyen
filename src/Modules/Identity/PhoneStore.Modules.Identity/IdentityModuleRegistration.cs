using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Infrastructure.Identity;
using PhoneStore.Modules.Identity.Application.Services;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.Modules.Identity;

public static class IdentityModuleRegistration
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=phonestore_db;Username=postgres;Password=postgres";

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString, x => x.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema)));

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddIdentityRateLimiting(configuration);

        // N2LHTBLTT-9: Phát JWT + lưu refresh token trong schema identity.
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();

        services.AddScoped<IdentityService>();
        services.AddScoped<IIdentityService>(provider => provider.GetRequiredService<IdentityService>());
        services.AddScoped<AccountRecoveryService>();
        services.Configure<RecoveryOptions>(configuration.GetSection("Recovery"));
        services.AddHttpClient("identity-sms").ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IRecoveryDelivery, RecoveryDelivery>();

        return services;
    }
}
