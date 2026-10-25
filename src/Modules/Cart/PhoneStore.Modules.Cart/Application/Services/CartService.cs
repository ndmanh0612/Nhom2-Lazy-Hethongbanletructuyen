using PhoneStore.BuildingBlocks.Contracts.Catalog;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Cart.Application.DTOs;
using PhoneStore.Modules.Cart.Domain.Entities;
using PhoneStore.Modules.Cart.Infrastructure.Repositories;
using CartEntity = PhoneStore.Modules.Cart.Domain.Entities.Cart;
using CartItemEntity = PhoneStore.Modules.Cart.Domain.Entities.CartItem;

namespace PhoneStore.Modules.Cart.Application.Services;

public interface ICartService
{
    Task<Result<CartDto>> GetCartAsync(long userId, CancellationToken cancellationToken = default);
    Task<Result<CartDto>> AddItemAsync(long userId, AddToCartRequest request, CancellationToken cancellationToken = default);
    Task<Result<CartDto>> UpdateItemAsync(long userId, long cartItemId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);
    Task<Result<CartDto>> RemoveItemAsync(long userId, long cartItemId, CancellationToken cancellationToken = default);
}

public class CartService : ICartService
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductPricingContract _pricingContract;

    public CartService(ICartRepository cartRepository, IProductPricingContract pricingContract)
    {
        _cartRepository = cartRepository;
        _pricingContract = pricingContract;
    }

    public async Task<Result<CartDto>> GetCartAsync(long userId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateActiveCartAsync(userId, cancellationToken);
        return Result.Success(ToDto(cart));
    }

    public async Task<Result<CartDto>> AddItemAsync(
        long userId,
        AddToCartRequest request,
        CancellationToken cancellationToken = default)
    {
        // Giá và thông tin sản phẩm luôn lấy từ Catalog, không tin vào dữ liệu client.
        var pricing = await _pricingContract.GetVariantPricingAsync(request.ProductVariantId, cancellationToken);
        if (pricing is null)
        {
            throw new NotFoundException("Không tìm thấy phiên bản sản phẩm hoặc sản phẩm đã ngừng bán.", "PRODUCT_VARIANT_NOT_FOUND");
        }

        var cart = await GetOrCreateActiveCartAsync(userId, cancellationToken);

        cart.AddItem(
            pricing.ProductVariantId,
            pricing.ProductName,
            pricing.Sku,
            pricing.Price,
            request.Quantity);

        await _cartRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(cart));
    }

    public async Task<Result<CartDto>> UpdateItemAsync(
        long userId,
        long cartItemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateActiveCartAsync(userId, cancellationToken);
        var cartItem = FindItem(cart, cartItemId);

        cart.UpdateItemQuantity(cartItem, request.Quantity);
        await _cartRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(cart));
    }

    public async Task<Result<CartDto>> RemoveItemAsync(
        long userId,
        long cartItemId,
        CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateActiveCartAsync(userId, cancellationToken);
        var cartItem = FindItem(cart, cartItemId);

        cart.RemoveItem(cartItem);
        await _cartRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(cart));
    }

    private async Task<CartEntity> GetOrCreateActiveCartAsync(long userId, CancellationToken cancellationToken)
    {
        var existing = await _cartRepository.GetActiveCartByUserAsync(userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var cart = CartEntity.Create(userId);
        await _cartRepository.AddAsync(cart, cancellationToken);
        await _cartRepository.SaveChangesAsync(cancellationToken);
        return cart;
    }

    private static CartItemEntity FindItem(CartEntity cart, long cartItemId)
    {
        var cartItem = cart.Items.FirstOrDefault(item => item.Id == cartItemId);
        if (cartItem is null)
        {
            throw new NotFoundException("Không tìm thấy sản phẩm trong giỏ hàng.", "CART_ITEM_NOT_FOUND");
        }

        return cartItem;
    }

    private static CartDto ToDto(CartEntity cart)
    {
        var items = cart.Items
            .OrderBy(item => item.Id)
            .Select(item => new CartItemDto(
                item.Id,
                item.ProductVariantId,
                item.ProductName,
                item.Sku,
                item.UnitPrice,
                item.Quantity,
                item.Subtotal))
            .ToList();

        return new CartDto(
            cart.Id,
            cart.UserId,
            cart.Status.ToString(),
            cart.TotalQuantity,
            cart.Subtotal,
            items,
            cart.CreatedAt,
            cart.UpdatedAt);
    }
}
