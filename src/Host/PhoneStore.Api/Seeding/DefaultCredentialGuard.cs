using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.Api.Seeding;

/// <summary>
/// Chặn các tài khoản mẫu có mật khẩu công khai (README, .env.example, DEMO_SCRIPT) trên Production.
/// <list type="bullet">
/// <item>Tài khoản mẫu còn dùng mật khẩu mặc định bị khoá (IsActive = false) và thu hồi mọi refresh token.</item>
/// <item><c>Security:BootstrapAdminPassword</c> (≥ 12 ký tự): nếu admin mẫu còn mật khẩu mặc định thì đổi sang
///   mật khẩu này và mở khoá — cách lấy lại quyền admin mà không cần mật khẩu công khai.</item>
/// <item><c>Security:AllowDemoAccounts=true</c>: bỏ qua bước khoá (chỉ dùng khi demo có kiểm soát).</item>
/// </list>
/// Chỉ đổi những tài khoản vẫn còn đúng mật khẩu mặc định; tài khoản đã đổi mật khẩu không bị đụng tới.
/// </summary>
internal static class DefaultCredentialGuard
{
    private const string AdminEmail = "admin@phonestore.vn";

    private static readonly Dictionary<string, string> PublishedDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        [AdminEmail] = "Admin@123",
        ["staff@phonestore.vn"] = "Staff@123",
        ["customer@phonestore.vn"] = "Customer@123",
        ["demo@phonestore.vn"] = "Demo@123",
    };

    public static async Task EnforceAsync(IServiceProvider services, IConfiguration configuration, ILogger logger,
        CancellationToken ct = default)
    {
        if (configuration.GetValue("Security:AllowDemoAccounts", false))
        {
            logger.LogWarning("Security:AllowDemoAccounts=true — tài khoản mẫu với mật khẩu công khai vẫn đăng nhập được.");
            return;
        }

        // Docker compose truyền biến chưa đặt thành chuỗi rỗng → coi như không đặt.
        var bootstrapPassword = configuration["Security:BootstrapAdminPassword"];
        if (string.IsNullOrWhiteSpace(bootstrapPassword))
        {
            bootstrapPassword = null;
        }
        else if (bootstrapPassword.Length < 12)
        {
            logger.LogError("Security:BootstrapAdminPassword phải dài ít nhất 12 ký tự — bỏ qua.");
            bootstrapPassword = null;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var emails = PublishedDefaults.Keys.Select(e => e.ToLowerInvariant()).ToList();
        var users = await db.Users.Where(u => emails.Contains(u.Email.ToLower())).ToListAsync(ct);
        var affected = new List<long>();

        foreach (var user in users)
        {
            var weak = hasher.VerifyPassword(PublishedDefaults[user.Email], user.PasswordHash)
                       || hasher.VerifyPassword(string.Empty, user.PasswordHash);
            if (!weak)
            {
                continue; // đã đổi sang mật khẩu riêng — an toàn
            }

            if (string.Equals(user.Email, AdminEmail, StringComparison.OrdinalIgnoreCase) && bootstrapPassword is not null)
            {
                user.UpdatePassword(hasher.HashPassword(bootstrapPassword));
                user.SetActive(true);
                logger.LogWarning("Đã đổi mật khẩu {Email} theo Security:BootstrapAdminPassword. Hãy xoá biến này khỏi env sau khi đăng nhập.", user.Email);
            }
            else if (user.IsActive)
            {
                user.SetActive(false);
                logger.LogWarning("Đã khoá {Email}: vẫn dùng mật khẩu mặc định công khai.", user.Email);
            }

            affected.Add(user.Id);
        }

        if (affected.Count == 0)
        {
            return;
        }

        var tokens = await db.RefreshTokens.Where(t => affected.Contains(t.UserId) && !t.IsRevoked).ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await db.SaveChangesAsync(ct);
    }
}
