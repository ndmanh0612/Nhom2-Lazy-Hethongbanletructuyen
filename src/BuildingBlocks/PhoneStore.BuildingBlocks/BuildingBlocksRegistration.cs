using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Infrastructure.EventBus;

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
                cfg.RegisterServicesFromAssembly(typeof(BuildingBlocksRegistration).Assembly);
            }
        });

        services.AddScoped<IEventBus, InMemoryEventBus>();

        return services;
    }
}
