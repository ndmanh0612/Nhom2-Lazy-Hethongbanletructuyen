using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;

namespace PhoneStore.Modules.Identity.Domain.Entities;

public class User : AggregateRoot<long>
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public UserRole Role { get; private set; } = UserRole.CUSTOMER;
    public bool IsActive { get; private set; } = true;

    private readonly List<Address> _addresses = [];
    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    protected User() { }

    public static User CreateCustomer(string email, string passwordHash, string fullName, string? phone = null)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return new User
        {
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Phone = phone?.Trim(),
            Role = UserRole.CUSTOMER,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public static User CreateStaff(string email, string passwordHash, string fullName, string? phone = null)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return new User
        {
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Phone = phone?.Trim(),
            Role = UserRole.STAFF,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// FR-15: Tạo tài khoản nhân viên với vai trò được chỉ định.
    /// Chỉ ADMIN được gọi method này (kiểm tra ở tầng endpoint).
    /// </summary>
    public static User CreateWithRole(
        string email,
        string passwordHash,
        string fullName,
        UserRole role,
        string? phone = null)
    {
        if (role == UserRole.CUSTOMER)
        {
            throw new ValidationException(
                "role",
                "Dùng CreateCustomer cho khách hàng; tài khoản nhân viên phải có vai trò STAFF hoặc ADMIN.");
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        return new User
        {
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Phone = phone?.Trim(),
            Role = role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateProfile(string fullName, string? phone)
    {
        FullName = fullName.Trim();
        Phone = phone?.Trim();
        Touch();
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    /// <summary>
    /// FR-15: Đổi vai trò của tài khoản.
    /// Không cho phép hạ cấp quyền của chính mình (chống khoá mình khỏi hệ thống).
    /// </summary>
    public void ChangeRole(UserRole newRole)
    {
        if (newRole == UserRole.CUSTOMER)
        {
            throw new ValidationException(
                "role",
                "Vai trò CUSTOMER dành cho khách hàng. Muốn hạ quyền nhân viên thì dùng API khóa tài khoản.");
        }

        if (Role == newRole)
        {
            return;
        }

        Role = newRole;
        Touch();
    }

    /// <summary>
    /// FR-15: Khóa / mở khóa tài khoản. Không cho phép tự khóa chính mình.
    /// </summary>
    public void Lock(bool isLocked)
    {
        if (isLocked && IsActive is false)
        {
            return;
        }

        SetActive(!isLocked);
    }
}
