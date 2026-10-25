using System.Transactions;
using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Contracts.Cart;
using PhoneStore.BuildingBlocks.Contracts.Catalog;
using PhoneStore.BuildingBlocks.Contracts.Order;
using PhoneStore.BuildingBlocks.Domain;
using PhoneStore.BuildingBlocks.Exceptions;
using PhoneStore.Modules.Order.Application.DTOs;
using PhoneStore.Modules.Order.Domain.Entities;
using PhoneStore.Modules.Order.Domain.Enums;
using PhoneStore.Modules.Order.Infrastructure.Repositories;
using OrderEntity = PhoneStore.Modules.Order.Domain.Entities.Order;

namespace PhoneStore.Modules.Order.Application.Services;

public interface IOrderService
{
    Task<Result<OrderDto>> CheckoutAsync(long customerId, CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<OrderDto>>> GetOrdersAsync(long customerId, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailDto>> GetOrderAsync(long orderId, long customerId, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailDto>> CancelOrderAsync(long orderId, long customerId, CancelOrderRequest request, CancellationToken cancellationToken = default);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICartContract _cartContract;
    private readonly IProductPricingContract _pricingContract;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public OrderService(
        IOrderRepository orderRepository,
        ICartContract cartContract,
        IProductPricingContract pricingContract,
        IDomainEventDispatcher domainEventDispatcher)
    {
        _orderRepository = orderRepository;
        _cartContract = cartContract;
        _pricingContract = pricingContract;
        _domainEventDispatcher = domainEventDispatcher;
    }

    /// <summary>
    /// FR-07: Checkout COD.
    /// Giỏ hàng (schema cart) + đơn hàng (schema ordering) nằm trong cùng transaction scope
    /// để khi tạo đơn thất bại thì giỏ hàng không bị chuyển trạng thái.
    /// </summary>
    public async Task<Result<OrderDto>> CheckoutAsync(
        long customerId,
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var cart = await _cartContract.GetActiveCartAsync(customerId, cancellationToken);
        if (cart is null || cart.IsEmpty)
        {
            throw new ValidationException("cart", "Giỏ hàng đang trống, không thể checkout.");
        }

        // Giá lấy lại từ Catalog lúc checkout để không dùng giá cũ trong giỏ.
        var itemDrafts = new List<OrderItemDraft>(cart.Items.Count);
        foreach (var item in cart.Items)
        {
            var pricing = await _pricingContract.GetVariantPricingAsync(item.ProductVariantId, cancellationToken);
            if (pricing is null)
            {
                throw new ValidationException(
                    "cart",
                    $"Sản phẩm \"{item.ProductName}\" không còn được bán. Vui lòng cập nhật giỏ hàng.");
            }

            itemDrafts.Add(new OrderItemDraft(
                pricing.ProductVariantId,
                pricing.ProductName,
                pricing.Sku,
                pricing.Price,
                item.Quantity));
        }

        var orderNumber = await GenerateOrderNumberAsync(cancellationToken);

        using var transactionScope = new TransactionScope(
            TransactionScopeAsyncFlowOption.Enabled);

        var order = OrderEntity.Create(
            orderNumber,
            customerId,
            request.ReceiverName,
            request.ReceiverPhone,
            request.ShippingAddress,
            request.Note,
            itemDrafts);

        await _orderRepository.AddAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        await _cartContract.MarkAsConvertedAsync(cart.CartId, cancellationToken);

        transactionScope.Complete();

        return Result.Success(ToDto(order));
    }

    public async Task<Result<IReadOnlyList<OrderDto>>> GetOrdersAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetByCustomerAsync(customerId, cancellationToken);
        return Result.Success<IReadOnlyList<OrderDto>>(orders.Select(ToDto).ToList());
    }

    public async Task<Result<OrderDetailDto>> GetOrderAsync(
        long orderId,
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new NotFoundException("Không tìm thấy đơn hàng.", "ORDER_NOT_FOUND");
        }

        if (order.CustomerId != customerId)
        {
            throw new ForbiddenException("Bạn không có quyền xem đơn hàng này.");
        }

        return Result.Success(ToDetailDto(order));
    }

    /// <summary>
    /// FR-09: Hủy đơn. Order phát OrderCancelled event, Inventory nhận event để hoàn tồn kho.
    /// </summary>
    public async Task<Result<OrderDetailDto>> CancelOrderAsync(
        long orderId,
        long customerId,
        CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new NotFoundException("Không tìm thấy đơn hàng.", "ORDER_NOT_FOUND");
        }

        if (order.CustomerId != customerId)
        {
            throw new ForbiddenException("Bạn không có quyền hủy đơn hàng này.");
        }

        order.Cancel(request.Reason);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        // Dispatch event sau khi đã commit dữ liệu đơn hàng.
        await _domainEventDispatcher.DispatchAsync(order.DomainEvents, cancellationToken);
        order.ClearDomainEvents();

        return Result.Success(ToDetailDto(order));
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            if (!await _orderRepository.ExistsOrderNumberAsync(candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Không thể sinh mã đơn hàng duy nhất.");
    }

    private static OrderDto ToDto(OrderEntity order)
    {
        return new OrderDto(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.Status.ToString(),
            order.PaymentMethod,
            order.Subtotal,
            order.ShippingFee,
            order.TotalAmount,
            order.ReceiverName,
            order.ReceiverPhone,
            order.ShippingAddress,
            order.Note,
            order.CancelReason,
            order.CancelledAt,
            order.DeliveredAt,
            order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemDto(
                    item.Id,
                    item.ProductVariantId,
                    item.ProductName,
                    item.Sku,
                    item.UnitPrice,
                    item.Quantity,
                    item.Subtotal))
                .ToList(),
            order.CreatedAt);
    }

    private static OrderDetailDto ToDetailDto(OrderEntity order)
    {
        return new OrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.Status.ToString(),
            order.PaymentMethod,
            order.Subtotal,
            order.ShippingFee,
            order.TotalAmount,
            order.ReceiverName,
            order.ReceiverPhone,
            order.ShippingAddress,
            order.Note,
            order.CancelReason,
            order.CancelledAt,
            order.DeliveredAt,
            order.Items
                .OrderBy(item => item.Id)
                .Select(item => new OrderItemDto(
                    item.Id,
                    item.ProductVariantId,
                    item.ProductName,
                    item.Sku,
                    item.UnitPrice,
                    item.Quantity,
                    item.Subtotal))
                .ToList(),
            order.StatusHistories
                .OrderBy(history => history.ChangedAt)
                .Select(history => new OrderStatusHistoryDto(
                    history.Id,
                    history.FromStatus?.ToString(),
                    history.ToStatus.ToString(),
                    history.Note,
                    history.ChangedAt))
                .ToList(),
            order.CreatedAt);
    }
}
