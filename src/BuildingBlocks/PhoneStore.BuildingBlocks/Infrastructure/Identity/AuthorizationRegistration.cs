using Microsoft.Extensions.DependencyInjection;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.BuildingBlocks.Infrastructure.Identity;

/// <summary>
/// N2LHTBLTT-16 / N2LHTBLTT-33: Policy-based authorization dùng chung cho toàn hệ thống.
/// Định nghĩa ở BuildingBlocks để các module không cần biết chi tiết vai trò,
/// và để test kiểm tra phân quyền Customer / Staff / Admin chỉ dựa vào policy.
/// </summary>
public static class AuthorizationRegistration
{
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.RequireAuthenticated,
                policy => policy.RequireAuthenticatedUser());

            options.AddPolicy(
                AuthorizationPolicies.RequireCustomer,
                policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Customer));

            options.AddPolicy(
                AuthorizationPolicies.RequireStaff,
                policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Staff, AppRoles.Admin));

            options.AddPolicy(
                AuthorizationPolicies.RequireAdmin,
                policy => policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
        });

        return services;
    }
}
