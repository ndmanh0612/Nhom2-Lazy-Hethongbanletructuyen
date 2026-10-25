using PhoneStore.BuildingBlocks.Domain;

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
}
