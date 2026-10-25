using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.Modules.Cart.Domain.Enums;

namespace PhoneStore.Modules.Cart.Domain.Entities;

public class Cart : AggregateRoot<long>
{
    public const int MaxItemsPerCart = 50;

    public long UserId { get; private set; }
    public CartStatus Status { get; private set; } = CartStatus.ACTIVE;

    private readonly List<CartItem> _items = [];
    public IReadOnlyCollection<CartItem> Items => _items.AsReadOnly();

    public int TotalQuantity => _items.Sum(item => item.Quantity);
    public long Subtotal => _items.Sum(item => item.Subtotal);

    protected Cart() { }

    public static Cart Create(long userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("Id khách hàng không hợp lệ.", nameof(userId));
        }

        return new Cart
        {
            UserId = userId,
            Status = CartStatus.ACTIVE,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public CartItem AddItem(long productVariantId, string productName, string sku, long unitPrice, int quantity)
    {
        if (Status != CartStatus.ACTIVE)
        {
            throw new InvalidOperationException("Chỉ giỏ hàng đang hoạt động mới được thêm sản phẩm.");
        }

        if (_items.Count >= MaxItemsPerCart && _items.All(item => item.ProductVariantId != productVariantId))
        {
            throw new InvalidOperationException($"Giỏ hàng không được vượt quá {MaxItemsPerCart} sản phẩm khác nhau.");
        }

        var existingItem = _items.FirstOrDefault(item => item.ProductVariantId == productVariantId);
        if (existingItem != null)
        {
            existingItem.IncreaseQuantity(quantity);
            Touch();
            return existingItem;
        }

        var cartItem = CartItem.Create(Id, productVariantId, productName, sku, unitPrice, quantity);
        _items.Add(cartItem);
        Touch();
        return cartItem;
    }

    public void UpdateItemQuantity(CartItem cartItem, int quantity)
    {
        EnsureItemBelongsToCart(cartItem);
        cartItem.SetQuantity(quantity);
        Touch();
    }

    public void RemoveItem(CartItem cartItem)
    {
        EnsureItemBelongsToCart(cartItem);
        _items.Remove(cartItem);
        Touch();
    }

    public void MarkAsConverted()
    {
        Status = CartStatus.CONVERTED;
        Touch();
    }

    public void MarkAsAbandoned()
    {
        Status = CartStatus.ABANDONED;
        Touch();
    }

    private void EnsureItemBelongsToCart(CartItem cartItem)
    {
        if (!_items.Contains(cartItem))
        {
            throw new InvalidOperationException("Sản phẩm không thuộc giỏ hàng hiện tại.");
        }
    }
}
