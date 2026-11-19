using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PhoneStore.BuildingBlocks.Common;

namespace PhoneStore.Modules.Identity.Infrastructure.Services;

public sealed class IdentityRateLimitOptions
{
    public int PermitLimit { get; set; } = 20;
    public int WindowSeconds { get; set; } = 60;
}

public static class IdentityRateLimiting
{
    public const string Policy = "identity-credentials";

    public static IServiceCollection AddIdentityRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<IdentityRateLimitOptions>()
            .Bind(configuration.GetSection("IdentityRateLimit"))
            .Validate(o => o.PermitLimit > 0 && o.WindowSeconds is > 0 and <= 3600, "Invalid IdentityRateLimit configuration.")
            .ValidateOnStart();
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<IdentityRateLimitOptions>>((options, limits) =>
        {
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.Value.PermitLimit,
                    Window = TimeSpan.FromSeconds(limits.Value.WindowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await context.HttpContext.Response.WriteAsJsonAsync(new ErrorResponse
                {
                    Code = "RATE_LIMIT_EXCEEDED",
                    Message = "Quá nhiều yêu cầu xác thực. Vui lòng thử lại sau.",
                    RequestId = context.HttpContext.TraceIdentifier,
                    Timestamp = DateTimeOffset.UtcNow
                }, cancellationToken);
            };
        });
        return services;
    }
}
