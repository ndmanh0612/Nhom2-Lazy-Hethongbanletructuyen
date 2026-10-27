using FluentAssertions;
using Xunit;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Domain.Entities;

namespace PhoneStore.UnitTests;

/// <summary>
/// FR-15 (N2LHTBLTT-16): Kiểm thử nghiệp vụ tài khoản ở tầng domain —
/// tạo tài khoản nhân viên, đổi vai trò, khóa / mở khóa.
/// </summary>
public class UserAccountManagementTests
{
    private const string Hash = "hash";

    [Fact]
    public void Create_customer_normalizes_email_to_lowercase()
    {
        var user = User.CreateCustomer("  Test@Example.COM  ", Hash, "Nguyễn Test");

        user.Email.Should().Be("test@example.com");
        user.Role.Should().Be(UserRole.CUSTOMER);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_with_role_customer_is_rejected()
    {
        var action = () => User.CreateWithRole("a@b.com", Hash, "Tên", UserRole.CUSTOMER);

        action.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Create_with_role_staff_is_allowed()
    {
        var user = User.CreateWithRole("staff@b.com", Hash, "Nhân Viên", UserRole.STAFF);

        user.Role.Should().Be(UserRole.STAFF);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_with_role_admin_is_allowed()
    {
        var user = User.CreateWithRole("admin@b.com", Hash, "Quản Trị", UserRole.ADMIN);

        user.Role.Should().Be(UserRole.ADMIN);
    }

    [Fact]
    public void Change_role_switches_from_staff_to_admin()
    {
        var user = User.CreateWithRole("staff@b.com", Hash, "Nhân Viên", UserRole.STAFF);

        user.ChangeRole(UserRole.ADMIN);

        user.Role.Should().Be(UserRole.ADMIN);
    }

    [Fact]
    public void Change_role_to_customer_is_rejected()
    {
        var user = User.CreateWithRole("staff@b.com", Hash, "Nhân Viên", UserRole.STAFF);

        var action = () => user.ChangeRole(UserRole.CUSTOMER);

        action.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Lock_deactivates_account()
    {
        var user = User.CreateCustomer("a@b.com", Hash, "Tên");

        user.Lock(isLocked: true);

        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Unlock_reactivates_account()
    {
        var user = User.CreateCustomer("a@b.com", Hash, "Tên");
        user.Lock(isLocked: true);

        user.Lock(isLocked: false);

        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_profile_changes_full_name_and_phone()
    {
        var user = User.CreateCustomer("a@b.com", Hash, "Tên Cũ", "0912345678");

        user.UpdateProfile("Tên Mới", "0999888777");

        user.FullName.Should().Be("Tên Mới");
        user.Phone.Should().Be("0999888777");
    }

    [Fact]
    public void Update_password_replaces_hash()
    {
        var user = User.CreateCustomer("a@b.com", "hash-cu", "Tên");

        user.UpdatePassword("hash-moi");

        user.PasswordHash.Should().Be("hash-moi");
    }
}
