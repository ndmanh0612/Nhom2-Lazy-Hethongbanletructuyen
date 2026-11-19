using PhoneStore.BuildingBlocks.Domain;

namespace PhoneStore.Modules.Catalog.Domain.Entities;

/// <summary>Đánh giá sản phẩm: mỗi người dùng một đánh giá cho mỗi sản phẩm (sửa được).</summary>
public class Review : BaseEntity<long>
{
    public long ProductId { get; private set; }
    public long UserId { get; private set; }
    public string ReviewerName { get; private set; } = string.Empty;
    public int Rating { get; private set; }
    public string Comment { get; private set; } = string.Empty;

    protected Review() { }

    public static Review Create(long productId, long userId, string reviewerName, int rating, string comment)
    {
        var review = new Review { ProductId = productId, UserId = userId };
        review.Update(reviewerName, rating, comment);
        return review;
    }

    public void Update(string reviewerName, int rating, string comment)
    {
        ReviewerName = reviewerName.Trim();
        Rating = rating;
        Comment = comment.Trim();
        Touch();
    }
}

/// <summary>Sản phẩm yêu thích của người dùng.</summary>
public class WishlistItem : BaseEntity<long>
{
    public long UserId { get; private set; }
    public long ProductId { get; private set; }

    protected WishlistItem() { }

    public static WishlistItem Create(long userId, long productId) => new() { UserId = userId, ProductId = productId };
}
