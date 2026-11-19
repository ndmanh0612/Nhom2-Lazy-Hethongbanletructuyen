using Microsoft.EntityFrameworkCore;
using Npgsql;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Domain.Entities;

namespace PhoneStore.Modules.Identity.Application.Services;

public partial class IdentityService
{
    public async Task<PagedResult<UserProfileDto>> GetCustomersAsync(CustomerFilterQuery query, CancellationToken ct)
    {
        CheckPage(query.Page, query.PageSize);
        var users = _dbContext.Users.AsNoTracking().Where(x => x.Role == UserRole.CUSTOMER);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            users = users.Where(x => x.Email.Contains(search) || x.FullName.ToLower().Contains(search)
                || (x.Phone != null && x.Phone.Contains(search)));
        }
        if (query.IsActive.HasValue) users = users.Where(x => x.IsActive == query.IsActive.Value);
        var count = await users.LongCountAsync(ct);
        var ordered = (query.Sort, query.Descending) switch
        {
            ("createdAt", false) => users.OrderBy(x => x.CreatedAt),
            ("createdAt", true) => users.OrderByDescending(x => x.CreatedAt),
            ("fullName", false) => users.OrderBy(x => x.FullName),
            ("fullName", true) => users.OrderByDescending(x => x.FullName),
            ("email", false) => users.OrderBy(x => x.Email),
            ("email", true) => users.OrderByDescending(x => x.Email),
            _ => throw new ValidationException("sort", "Trường sắp xếp không hợp lệ.")
        };
        var page = await ordered.ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return PagedResult<UserProfileDto>.Create(page.Select(ToProfileDto).ToList(), query.Page, query.PageSize, count);
    }

    public async Task<CustomerDetailDto> GetCustomerAsync(long id, CancellationToken ct)
    {
        var user = await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Role == UserRole.CUSTOMER, ct)
            ?? throw CustomerMissing();
        var addresses = (await GetAddressesAsync(id, ct)).Value;
        return new(ToProfileDto(user), addresses);
    }

    public async Task<UserProfileDto> CreateCustomerAsync(RegisterRequest request, long actorId, CancellationToken ct)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LockAdministrationAsync(actorId, ct);
        var user = User.CreateCustomer(request.Email, _passwordHasher.HashPassword(request.Password), request.FullName, request.Phone);
        _dbContext.Users.Add(user);
        try { await _dbContext.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_Email" })
        { throw new ConflictException("Email này đã được sử dụng.", "EMAIL_ALREADY_EXISTS"); }
        await WriteAuditAsync(actorId, user.Id, "CustomerCreated", null, ct);
        await tx.CommitAsync(ct);
        return ToProfileDto(user);
    }

    public async Task<UserProfileDto> UpdateCustomerAsync(long id, UpdateCustomerRequest request, long actorId, CancellationToken ct)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LockAdministrationAsync(actorId, ct);
        var user = await RequireCustomerForUpdateAsync(id, ct);
        if (user.UpdatedAt != request.ExpectedUpdatedAt)
            throw new ConflictException("Hồ sơ đã thay đổi. Hãy tải lại trước khi cập nhật.", "CUSTOMER_CHANGED");
        user.UpdateProfile(request.FullName, request.Phone);
        await InvalidateChallengesAsync(id, ct);
        await WriteAuditAsync(actorId, id, "CustomerUpdated", request.Reason, ct);
        await tx.CommitAsync(ct);
        return ToProfileDto(user);
    }

    public async Task<UserProfileDto> SetCustomerLockAsync(long id, LockAccountRequest request, long actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("reason", "Cần lý do khóa/mở tài khoản.");
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LockAdministrationAsync(actorId, ct);
        var user = await RequireCustomerForUpdateAsync(id, ct);
        user.Lock(request.IsLocked);
        if (request.IsLocked) await RevokeAllSessionsAsync(id, ct);
        await WriteAuditAsync(actorId, id, request.IsLocked ? "CustomerLocked" : "CustomerUnlocked", request.Reason, ct);
        await tx.CommitAsync(ct);
        return ToProfileDto(user);
    }

    public async Task RevokeCustomerSessionsAsync(long id, long actorId, CancellationToken ct)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LockAdministrationAsync(actorId, ct);
        await RequireCustomerForUpdateAsync(id, ct);
        await RevokeAllSessionsAsync(id, ct);
        await WriteAuditAsync(actorId, id, "CustomerSessionsRevoked", null, ct);
        await tx.CommitAsync(ct);
    }

    public async Task ResetCustomerPasswordAsync(long id, ResetPasswordRequest request, long actorId, CancellationToken ct)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        await LockAdministrationAsync(actorId, ct);
        var user = await RequireCustomerForUpdateAsync(id, ct);
        user.UpdatePassword(_passwordHasher.HashPassword(request.NewPassword));
        await RevokeAllSessionsAsync(id, ct);
        await WriteAuditAsync(actorId, id, "CustomerPasswordReset", null, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<PagedResult<AuditDto>> GetAuditAsync(AuditFilterQuery query, CancellationToken ct)
    {
        CheckPage(query.Page, query.PageSize);
        var entries = _dbContext.AuditEntries.AsNoTracking().AsQueryable();
        if (query.ActorId.HasValue) entries = entries.Where(x => x.ActorId == query.ActorId);
        if (query.TargetId.HasValue) entries = entries.Where(x => x.TargetId == query.TargetId);
        if (!string.IsNullOrWhiteSpace(query.Action)) entries = entries.Where(x => x.Action == query.Action);
        if (query.From.HasValue)
        {
            var from = query.From.Value.ToUniversalTime();
            entries = entries.Where(x => x.CreatedAt >= from);
        }
        if (query.To.HasValue)
        {
            var to = query.To.Value.ToUniversalTime();
            entries = entries.Where(x => x.CreatedAt <= to);
        }
        var count = await entries.LongCountAsync(ct);
        var rows = await entries.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new AuditDto(x.Id, x.ActorId, x.TargetId, x.Action, x.Reason, x.CreatedAt)).ToListAsync(ct);
        return PagedResult<AuditDto>.Create(rows, query.Page, query.PageSize, count);
    }

    private async Task<User> RequireCustomerForUpdateAsync(long id, CancellationToken ct)
    {
        var user = await LockUserAsync(id, ct);
        return user is not null && user.Role == UserRole.CUSTOMER ? user : throw CustomerMissing();
    }
    private static NotFoundException CustomerMissing() => new("Không tìm thấy khách hàng.", "CUSTOMER_NOT_FOUND");
    private static void CheckPage(int page, int size)
    {
        if (page < 1 || size is < 1 or > 100 || ((long)page - 1) * size > int.MaxValue)
            throw new ValidationException("page", "Phân trang không hợp lệ.");
    }
    private async Task WriteAuditAsync(long actorId, long targetId, string action, string? reason, CancellationToken ct)
    {
        _dbContext.AuditEntries.Add(IdentityAudit.Create(actorId, targetId, action, reason));
        await _dbContext.SaveChangesAsync(ct);
    }
    private Task InvalidateChallengesAsync(long userId, CancellationToken ct) => _dbContext.VerificationChallenges
        .Where(x => x.UserId == userId && x.ConsumedAt == null)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAt, DateTimeOffset.UtcNow), ct);
}
