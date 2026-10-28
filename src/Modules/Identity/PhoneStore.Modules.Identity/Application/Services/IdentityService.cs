using Microsoft.EntityFrameworkCore;
using Npgsql;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.BuildingBlocks.Infrastructure.Identity;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Domain.Entities;
using PhoneStore.Modules.Identity.Infrastructure.Persistence;
using PhoneStore.Modules.Identity.Infrastructure.Services;

namespace PhoneStore.Modules.Identity.Application.Services;

public interface IIdentityService
{
    Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthResponseDto>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default, string? sessionId = null);
    Task<Result<UserProfileDto>> GetProfileAsync(long userId, CancellationToken cancellationToken = default);
    Task<Result<UserProfileDto>> UpdateProfileAsync(long userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<Result> ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AddressDto>>> GetAddressesAsync(long userId, CancellationToken cancellationToken = default);
    Task<Result<AddressDto>> AddAddressAsync(long userId, CreateAddressRequest request, CancellationToken cancellationToken = default);

    // FR-15 (N2LHTBLTT-16): Quản lý tài khoản nhân viên.
    Task<Result<UserProfileDto>> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<UserProfileDto>>> GetStaffAsync(StaffFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<UserProfileDto>> ChangeRoleAsync(long userId, ChangeRoleRequest request, long actorId, CancellationToken cancellationToken = default);
    Task<Result<UserProfileDto>> LockAccountAsync(long userId, LockAccountRequest request, long actorId, CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(long userId, ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

public class IdentityService : IIdentityService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;

    public IdentityService(
        IdentityDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IRefreshTokenStore refreshTokenStore)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenStore = refreshTokenStore;
    }

    // -----------------------------------------------------------------------
    // FR-01 / FR-02: Đăng ký, đăng nhập, làm mới token, đăng xuất.
    // -----------------------------------------------------------------------

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

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        _dbContext.Users.Add(user);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_Email" })
        {
            throw new ConflictException("Email này đã được sử dụng trong hệ thống.", "EMAIL_ALREADY_EXISTS");
        }

        var tokenPair = await _tokenService.CreateTokenPairAsync(
            user.Id,
            user.Email,
            user.Role.ToString(),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(ToAuthDto(user, tokenPair));
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Cùng khóa tài khoản với các thao tác đổi mật khẩu/khóa tài khoản.
        var user = await _dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Email\" = {normalizedEmail} FOR UPDATE")
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

        var tokenPair = await _tokenService.CreateTokenPairAsync(
            user.Id,
            user.Email,
            user.Role.ToString(),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(ToAuthDto(user, tokenPair));
    }

    /// <summary>
    /// FR-02: Làm mới access token bằng refresh token.
    /// Refresh token cũ bị thu hồi ngay sau khi dùng (token rotation) — nếu
    /// client gửi lại token đã dùng thì coi như token bị đánh cắp và từ chối.
    /// </summary>
    public async Task<Result<AuthResponseDto>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException("Thiếu refresh token.", "REFRESH_TOKEN_MISSING");
        }

        var tokenHash = JwtTokenService.HashToken(refreshToken);
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Luôn khóa user trước token để nhất quán với thu hồi toàn bộ phiên.
        var user = await _dbContext.Users.FromSqlInterpolated($"""
            SELECT * FROM identity.users WHERE "Id" =
                (SELECT "UserId" FROM identity.refresh_tokens WHERE "TokenHash" = {tokenHash})
            FOR UPDATE
            """).FirstOrDefaultAsync(cancellationToken);
        var userId = await _refreshTokenStore.ConsumeAsync(tokenHash, cancellationToken);

        if (userId is null || user is null)
        {
            throw new UnauthorizedException(
                "Refresh token không hợp lệ hoặc đã hết hạn. Vui lòng đăng nhập lại.",
                "REFRESH_TOKEN_INVALID");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException("Tài khoản của bạn đã bị khóa.", "ACCOUNT_LOCKED");
        }

        var newTokenPair = await _tokenService.CreateTokenPairAsync(
            user.Id,
            user.Email,
            user.Role.ToString(),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(ToAuthDto(user, newTokenPair));
    }

    /// <summary>
    /// FR-02: Thu hồi phiên; access token tham chiếu phiên cũng bị từ chối.
    /// </summary>
    public async Task<Result> LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default, string? sessionId = null)
    {
        var tokenHash = !string.IsNullOrWhiteSpace(refreshToken)
            ? JwtTokenService.HashToken(refreshToken)
            : sessionId;
        if (!string.IsNullOrWhiteSpace(tokenHash))
        {
            await _refreshTokenStore.RevokeAsync(tokenHash, cancellationToken);
        }

        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // FR-03: Hồ sơ, mật khẩu, địa chỉ.
    // -----------------------------------------------------------------------

    public async Task<Result<UserProfileDto>> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy thông tin người dùng.", "USER_NOT_FOUND");
        }

        return Result.Success(ToProfileDto(user));
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

        return Result.Success(ToProfileDto(user));
    }

    public async Task<Result> ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId, cancellationToken);
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

        // Đổi mật khẩu phải thu hồi mọi phiên đang mở (N2LHTBLTT-38).
        await RevokeAllSessionsAsync(userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
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

    // -----------------------------------------------------------------------
    // FR-15 (N2LHTBLTT-16): Quản lý tài khoản nhân viên.
    // -----------------------------------------------------------------------

    public async Task<Result<UserProfileDto>> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            throw new ConflictException("Email này đã được sử dụng trong hệ thống.", "EMAIL_ALREADY_EXISTS");
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new ValidationException("role", "Vai trò không hợp lệ. Chỉ nhận STAFF hoặc ADMIN.");
        }

        var user = User.CreateWithRole(
            normalizedEmail,
            _passwordHasher.HashPassword(request.Password),
            request.FullName,
            role,
            request.Phone);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(ToProfileDto(user));
    }

    public async Task<Result<PagedResult<UserProfileDto>>> GetStaffAsync(StaffFilterQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role != UserRole.CUSTOMER);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(u =>
                u.FullName.ToLower().Contains(search) || u.Email.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            if (!Enum.TryParse<UserRole>(query.Role, ignoreCase: true, out var role))
            {
                throw new ValidationException("role", "Vai trò không hợp lệ. Chỉ nhận STAFF hoặc ADMIN.");
            }

            baseQuery = baseQuery.Where(u => u.Role == role);
        }

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var users = await baseQuery
            .OrderBy(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = users.Select(ToProfileDto).ToList();

        return Result.Success(PagedResult<UserProfileDto>.Create(dtos, page, pageSize, totalCount));
    }

    public async Task<Result<UserProfileDto>> ChangeRoleAsync(long userId, ChangeRoleRequest request, long actorId, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new ValidationException("role", "Vai trò không hợp lệ. Chỉ nhận STAFF hoặc ADMIN.");
        }

        if (userId == actorId)
        {
            throw new ValidationException("userId", "Không thể tự đổi vai trò của chính mình.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy tài khoản.", "USER_NOT_FOUND");
        }

        user.ChangeRole(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Vai trò cũ không còn hợp lệ nên phải đăng nhập lại để lấy claim mới.
        await RevokeAllSessionsAsync(userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(ToProfileDto(user));
    }

    public async Task<Result<UserProfileDto>> LockAccountAsync(long userId, LockAccountRequest request, long actorId, CancellationToken cancellationToken = default)
    {
        if (userId == actorId)
        {
            throw new ValidationException("userId", "Không thể tự khóa tài khoản của chính mình.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy tài khoản.", "USER_NOT_FOUND");
        }

        user.Lock(request.IsLocked);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.IsLocked)
        {
            // Khoá tài khoản phải chấm dứt mọi phiên đang mở.
            await RevokeAllSessionsAsync(userId, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success(ToProfileDto(user));
    }

    public async Task<Result> ResetPasswordAsync(long userId, ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockUserAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy tài khoản.", "USER_NOT_FOUND");
        }

        user.UpdatePassword(_passwordHasher.HashPassword(request.NewPassword));
        await _dbContext.SaveChangesAsync(cancellationToken);

        await RevokeAllSessionsAsync(userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private Task<User?> LockUserAsync(long userId, CancellationToken cancellationToken) =>
        _dbContext.Users.FromSqlInterpolated($"SELECT * FROM identity.users WHERE \"Id\" = {userId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// N2LHTBLTT-38: Thu hồi toàn bộ phiên của một người dùng — dùng khi đổi
    /// mật khẩu, khoá tài khoản hoặc đổi vai trò.
    /// </summary>
    private async Task RevokeAllSessionsAsync(long userId, CancellationToken cancellationToken)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            return;
        }

        foreach (var token in tokens)
        {
            token.Revoke();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuthResponseDto ToAuthDto(User user, TokenPair tokenPair)
    {
        return new AuthResponseDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Role.ToString(),
            tokenPair.AccessToken,
            tokenPair.ExpiresAt)
        {
            RefreshToken = tokenPair.RefreshToken
        };
    }

    private static UserProfileDto ToProfileDto(User user)
    {
        return new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.Phone,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt);
    }
}
