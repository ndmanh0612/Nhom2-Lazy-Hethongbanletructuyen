using Microsoft.EntityFrameworkCore;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.Api.Seeding;

/// <summary>
/// Tài khoản khách công khai: ai cũng đăng nhập được (vai trò CUSTOMER, không có quyền quản trị).
/// Chạy mỗi lần khởi động: tạo nếu chưa có, và luôn đặt lại mật khẩu + mở khoá để không ai khoá được tài khoản này.
/// </summary>
internal static class GuestAccountSeeder
{
    public const string Email = "guest@phonestore.vn";
    public const string Password = "Guest@123";

    public static async Task EnsureAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == Email, ct);
        if (user is null)
        {
            db.Users.Add(User.CreateCustomer(Email, hasher.HashPassword(Password), "Khách", null));
        }
        else
        {
            if (!hasher.VerifyPassword(Password, user.PasswordHash))
            {
                user.UpdatePassword(hasher.HashPassword(Password));
            }
            if (!user.IsActive)
            {
                user.SetActive(true);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
