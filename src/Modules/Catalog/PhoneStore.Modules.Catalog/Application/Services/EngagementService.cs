using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Catalog.Domain.Entities;
using PhoneStore.Modules.Catalog.Infrastructure.Persistence;

namespace PhoneStore.Modules.Catalog.Application.Services;

public record ReviewDto(long Id, long UserId, string ReviewerName, int Rating, string Comment, DateTimeOffset CreatedAt);
public record ReviewSummaryDto(double AverageRating, int Count, IReadOnlyList<ReviewDto> Items);
public record UpsertReviewRequest(string? ReviewerName, int Rating, string? Comment);

public interface IEngagementService
{
    Task<ReviewSummaryDto> GetReviewsAsync(long productId, CancellationToken ct = default);
    Task<ReviewDto> UpsertReviewAsync(long productId, long userId, UpsertReviewRequest request, CancellationToken ct = default);
    Task DeleteReviewAsync(long productId, long userId, CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetWishlistAsync(long userId, CancellationToken ct = default);
    Task AddToWishlistAsync(long userId, long productId, CancellationToken ct = default);
    Task RemoveFromWishlistAsync(long userId, long productId, CancellationToken ct = default);
}

public class EngagementService : IEngagementService
{
    private readonly CatalogDbContext _db;

    public EngagementService(CatalogDbContext db) => _db = db;

    public async Task<ReviewSummaryDto> GetReviewsAsync(long productId, CancellationToken ct = default)
    {
        await EnsureProductAsync(productId, ct);
        var items = await _db.Reviews.AsNoTracking()
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.UpdatedAt)
            .Take(100)
            .ToListAsync(ct);
        var count = await _db.Reviews.CountAsync(r => r.ProductId == productId, ct);
        var avg = count == 0 ? 0 : await _db.Reviews.Where(r => r.ProductId == productId).AverageAsync(r => (double)r.Rating, ct);
        return new ReviewSummaryDto(Math.Round(avg, 1), count, items.Select(ToDto).ToList());
    }

    public async Task<ReviewDto> UpsertReviewAsync(long productId, long userId, UpsertReviewRequest request, CancellationToken ct = default)
    {
        await EnsureProductAsync(productId, ct);
        if (request.Rating is < 1 or > 5)
        {
            throw new ValidationException("rating", "Số sao phải từ 1 đến 5.");
        }

        var comment = (request.Comment ?? string.Empty).Trim();
        if (comment.Length > 1000)
        {
            throw new ValidationException("comment", "Nội dung đánh giá tối đa 1000 ký tự.");
        }

        var name = string.IsNullOrWhiteSpace(request.ReviewerName) ? "Khách hàng" : request.ReviewerName.Trim();
        if (name.Length > 100) name = name[..100];

        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId, ct);
        if (review is null)
        {
            review = Review.Create(productId, userId, name, request.Rating, comment);
            _db.Reviews.Add(review);
        }
        else
        {
            review.Update(name, request.Rating, comment);
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(review);
    }

    public async Task DeleteReviewAsync(long productId, long userId, CancellationToken ct = default)
    {
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId, ct);
        if (review is null)
        {
            throw new NotFoundException("Bạn chưa đánh giá sản phẩm này.", "REVIEW_NOT_FOUND");
        }

        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<long>> GetWishlistAsync(long userId, CancellationToken ct = default)
        => await _db.WishlistItems.AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => w.ProductId)
            .ToListAsync(ct);

    public async Task AddToWishlistAsync(long userId, long productId, CancellationToken ct = default)
    {
        await EnsureProductAsync(productId, ct);
        if (await _db.WishlistItems.AnyAsync(w => w.UserId == userId && w.ProductId == productId, ct))
        {
            return; // idempotent
        }

        _db.WishlistItems.Add(WishlistItem.Create(userId, productId));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Hai request đồng thời cùng thêm: unique index chặn bản trùng, coi như đã thêm.
        }
    }

    public async Task RemoveFromWishlistAsync(long userId, long productId, CancellationToken ct = default)
    {
        await _db.WishlistItems.Where(w => w.UserId == userId && w.ProductId == productId).ExecuteDeleteAsync(ct);
    }

    private async Task EnsureProductAsync(long productId, CancellationToken ct)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId && p.IsActive, ct))
        {
            throw new NotFoundException("Không tìm thấy sản phẩm.", "PRODUCT_NOT_FOUND");
        }
    }

    private static ReviewDto ToDto(Review r) => new(r.Id, r.UserId, r.ReviewerName, r.Rating, r.Comment, r.CreatedAt);
}
