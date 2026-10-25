using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.Modules.Identity.Application.Services;

public interface IIdentityService
{
    Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserProfileDto>> GetProfileAsync(long userId, CancellationToken cancellationToken = default);
    Task<Result<UserProfileDto>> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<Result> ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AddressDto>>> GetAddressesAsync(long userId, CancellationToken cancellationToken = default);
    Task<Result<AddressDto>> AddAddressAsync(long userId, CreateAddressRequest request, CancellationToken cancellationToken = default);
}

public class IdentityService : IIdentityService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    public IdentityService(IdentityDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existingUser = await _dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (existingUser)
        {
            throw new ConflictException("Email này đã được sử dụng trong hệ thống.", "EMAIL_ALREADY_EXISTS");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = User.CreateCustomer(normalizedEmail, passwordHash, request.FullName, request.Phone);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Dummy session / token for base initialization
        var token = Guid.NewGuid().ToString("N");
        return Result.Success(new AuthResponseDto(user.Id, user.Email, user.FullName, user.Role.ToString(), token));
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // NFR-01: Thông báo đăng nhập sai không tiết lộ email có tồn tại
        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Email hoặc mật khẩu không chính xác.", "INVALID_CREDENTIALS");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.", "ACCOUNT_LOCKED");
        }

        var token = Guid.NewGuid().ToString("N");
        return Result.Success(new AuthResponseDto(user.Id, user.Email, user.FullName, user.Role.ToString(), token));
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy thông tin người dùng.", "USER_NOT_FOUND");
        }

        return Result.Success(new UserProfileDto(user.Id, user.Email, user.FullName, user.Phone, user.Role.ToString(), user.IsActive, user.CreatedAt));
    }

    public async Task<Result<UserProfileDto>> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy người dùng.", "USER_NOT_FOUND");
        }

        user.UpdateProfile(request.FullName, request.Phone);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new UserProfileDto(user.Id, user.Email, user.FullName, user.Phone, user.Role.ToString(), user.IsActive, user.CreatedAt));
    }

    public async Task<Result> ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy người dùng.", "USER_NOT_FOUND");
        }

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            throw new ValidationException("currentPassword", "Mật khẩu hiện tại không chính xác.");
        }

        var newHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatePassword(newHash);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<AddressDto>>> GetAddressesAsync(long userId, CancellationToken cancellationToken = default)
    {
        var addresses = await _dbContext.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AddressDto(a.Id, a.UserId, a.ReceiverName, a.ReceiverPhone, a.AddressText, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AddressDto>>(addresses);
    }

    public async Task<Result<AddressDto>> AddAddressAsync(long userId, CreateAddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = Address.Create(userId, request.ReceiverName, request.ReceiverPhone, request.AddressText);
        _dbContext.Addresses.Add(address);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new AddressDto(address.Id, address.UserId, address.ReceiverName, address.ReceiverPhone, address.AddressText, address.CreatedAt));
    }
}
