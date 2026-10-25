using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Infrastructure.EventBus;
using PhoneStore.BuildingBlocks.Infrastructure.Identity;

namespace PhoneStore.BuildingBlocks;

public static class BuildingBlocksRegistration
{
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services, params Type[] markerTypes)
    {
        var assemblies = markerTypes.Select(t => t.Assembly).Distinct().ToArray();

        services.AddMediatR(cfg =>
        {
            if (assemblies.Length > 0)
            {
                cfg.RegisterServicesFromAssemblies(assemblies);
            }
            else
            {
                cfg.RegisterServicesFromAssemblies(typeof(BuildingBlocksRegistration).Assembly);
            }
        });

        services.AddScoped<IEventBus, InMemoryEventBus>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        return services;
    }
}
