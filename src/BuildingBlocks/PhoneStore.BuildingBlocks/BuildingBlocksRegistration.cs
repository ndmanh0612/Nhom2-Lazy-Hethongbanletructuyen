using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Infrastructure.EventBus;
using PhoneStore.BuildingBlocks.Infrastructure.Identity;

namespace PhoneStore.BuildingBlocks;

public static class BuildingBlocksRegistration
{
    public static IServiceCollection AddBuildingBlocks(
        this IServiceCollection services,
        IConfiguration configuration,
        params Type[] markerTypes)
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

        // N2LHTBLTT-9: Cấu hình JWT bearer làm cơ chế xác thực chính của API.
        services.AddAppAuthentication(configuration);

        // N2LHTBLTT-16 / N2LHTBLTT-33: Policy phân quyền theo vai trò.
        services.AddAppAuthorization();

        return services;
    }

    /// <summary>
    /// N2LHTBLTT-9: Đăng ký JWT bearer authentication đọc cấu hình từ
    /// mục "Authentication" của appsettings.
    /// </summary>
    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var signingKey = jwtOptions.ResolveSigningKey();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = "email"
                };

                // N2LHTBLTT-38: 401 / 403 phải trả về đúng envelope ErrorResponse
                // giống các lỗi nghiệp vụ, để client chỉ cần xử lý một cấu trúc.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json; charset=utf-8";

                        await context.Response.WriteAsJsonAsync(new ErrorResponse
                        {
                            Code = "UNAUTHORIZED",
                            Message = "Chưa được xác thực hoặc phiên đăng nhập đã hết hạn.",
                            RequestId = context.HttpContext.TraceIdentifier,
                            Timestamp = DateTimeOffset.UtcNow
                        });
                    },
                    OnForbidden = async context =>
                    {
                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json; charset=utf-8";

                        await context.Response.WriteAsJsonAsync(new ErrorResponse
                        {
                            Code = "FORBIDDEN",
                            Message = "Bạn không có quyền thực hiện thao tác này.",
                            RequestId = context.HttpContext.TraceIdentifier,
                            Timestamp = DateTimeOffset.UtcNow
                        });
                    }
                };
            });

        // N2LHTBLTT-38: Antiforgery dùng cho cơ chế CSRF.
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AuthHeaders.CsrfToken;
            options.Cookie.Name = AuthCookies.CsrfToken;
        });

        return services;
    }
}
