using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Infrastructure.Web;

namespace PhoneStore.BuildingBlocks.Validation;

public static class ValidationRegistration
{
    /// <summary>
    /// Đăng ký FluentValidation cho toàn hệ thống:
    /// - Quét validator trong các assembly được truyền vào.
    /// - Gắn filter validate request + chuẩn hóa lỗi ModelState.
    /// </summary>
    public static IServiceCollection AddBuildingBlocksValidation(
        this IServiceCollection services,
        params Type[] markerTypes)
    {
        var assemblies = markerTypes.Select(type => type.Assembly).Distinct().ToArray();

        services.AddValidatorsFromAssemblies(assemblies);

        services.AddControllers(options =>
        {
            options.Filters.Add<StandardModelStateFilter>();
            options.Filters.Add<FluentValidationFilter>();
        });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = InvalidModelStateResponseFactory.Create;
        });

        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

        return services;
    }
}
