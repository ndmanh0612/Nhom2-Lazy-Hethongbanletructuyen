using System.Transactions;
using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Contracts.Cart;
using PhoneStore.BuildingBlocks.Contracts.Catalog;
using PhoneStore.BuildingBlocks.Contracts.Inventory;
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
    Task<Result<PagedResult<OrderDto>>> GetOrdersAsync(long customerId, OrderFilterQuery query, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailDto>> GetOrderAsync(long orderId, long customerId, bool isStaff = false, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailDto>> CancelOrderAsync(long orderId, long customerId, CancelOrderRequest request, CancellationToken cancellationToken = default);

    // FR-10 (N2LHTBLTT-27): Xác nhận và giao hàng.
    Task<Result<OrderDetailDto>> ConfirmOrderAsync(long orderId, ConfirmOrderRequest request, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailDto>> ShipOrderAsync(long orderId, ShipOrderRequest request, CancellationToken cancellationToken = default);

    // FR-11 (N2LHTBLTT-28): Giao thành công / thất bại.
    Task<Result<OrderDetailDto>> DeliverOrderAsync(long orderId, DeliverOrderRequest request, CancellationToken cancellationToken = default);
    Task<Result<OrderDetailDto>> FailDeliveryAsync(long orderId, DeliveryFailedRequest request, CancellationToken cancellationToken = default);

    // FR-11: Danh sách đơn COD đã thu tiền.
    Task<Result<PagedResult<CodCollectionDto>>> GetCodCollectionsAsync(OrderFilterQuery query, CancellationToken cancellationToken = default);
}

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICartContract _cartContract;
    private readonly IProductPricingContract _pricingContract;
    private readonly IInventoryContract _inventoryContract;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public OrderService(
        IOrderRepository orderRepository,
        ICartContract cartContract,
        IProductPricingContract pricingContract,
        IInventoryContract inventoryContract,
        IDomainEventDispatcher domainEventDispatcher)
    {
        _orderRepository = orderRepository;
        _cartContract = cartContract;
        _pricingContract = pricingContract;
        _inventoryContract = inventoryContract;
        _domainEventDispatcher = domainEventDispatcher;
    }

    /// <summary>
    /// FR-07: Checkout COD.
    ///
    /// N2LHTBLTT-22 / N2LHTBLTT-24: toàn bộ thao tác nằm trong một transaction
    /// scope (cart + order + inventory) để:
    /// - đặt giữ tồn kho trước, thất bại thì rollback và không tạo đơn;
    /// - giỏ hàng chỉ chuyển sang CONVERTED sau khi đơn đã lưu thành công;
    /// - nếu bất kỳ bước nào ném lỗi thì transaction bị hủy và dữ liệu giữ nguyên.
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

        // N2LHTBLTT-24: chống tạo trùng đơn — nếu giỏ hàng đang ACTIVE nhưng đã
        // có đơn được tạo từ chính giỏ này (do retry request) thì trả về đơn cũ.
        var existingOrder = await _orderRepository.GetByCartIdAsync(cart.CartId, cancellationToken);
        if (existingOrder is not null)
        {
            throw new ConflictException(
                "Giỏ hàng này đã được chuyển thành đơn hàng. Vui lòng kiểm tra danh sách đơn hàng.",
                "DUPLICATE_ORDER");
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
            TransactionScopeOption.Required,
            new TransactionOptions
            {
                IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
                Timeout = TimeSpan.FromSeconds(30)
            },
            TransactionScopeAsyncFlowOption.Enabled);

        // N2LHTBLTT-22: đặt giữ tồn kho với khóa dòng (FOR UPDATE) trong module
        // Inventory. Không đủ tồn thì trả false → rollback toàn bộ checkout.
        var stockItems = itemDrafts
            .Select(draft => new StockChangeItem(draft.ProductVariantId, draft.Quantity))
            .ToList();

        // Tạo đơn trước để có OrderId, nhờ vậy biến động tồn kho ghi được
        // liên kết với đơn hàng (truy vết đối soát).
        var order = OrderEntity.Create(
            orderNumber,
            customerId,
            request.ReceiverName,
            request.ReceiverPhone,
            request.ShippingAddress,
            request.Note,
            itemDrafts,
            cart.CartId);

        await _orderRepository.AddAsync(order, cancellationToken);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        var reserved = await _inventoryContract.TryReserveStockAsync(stockItems, order.Id, cancellationToken);
        if (!reserved)
        {
            throw new ConflictException(
                "Tồn kho không đủ để thực hiện đơn hàng. Vui lòng kiểm tra lại số lượng.",
                "INSUFFICIENT_STOCK");
        }

        await _cartContract.MarkAsConvertedAsync(cart.CartId, cancellationToken);

        transactionScope.Complete();

        return Result.Success(ToDto(order));
    }

    /// <summary>
    /// FR-08: Danh sách đơn hàng của khách hàng, hỗ trợ lọc theo trạng thái / thời gian và phân trang.
    /// </summary>
    public async Task<Result<PagedResult<OrderDto>>> GetOrdersAsync(
        long customerId,
        OrderFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var paged = await _orderRepository.GetByCustomerPagedAsync(customerId, query, cancellationToken);

        var dtos = paged.Items.Select(ToDto).ToList();
        var result = PagedResult<OrderDto>.Create(dtos, paged.Page, paged.PageSize, paged.TotalCount);

        return Result.Success(result);
    }

    public async Task<Result<OrderDetailDto>> GetOrderAsync(
        long orderId,
        long customerId,
        bool isStaff = false,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new NotFoundException("Không tìm thấy đơn hàng.", "ORDER_NOT_FOUND");
        }

        // N2LHTBLTT-27: nhan vien (STAFF/ADMIN) can xem don cua khach hang de thuc
        // hien xac nhan / giao hang / thu tien COD.
        if (!isStaff && order.CustomerId != customerId)
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

    // -----------------------------------------------------------------------
    // FR-10 (N2LHTBLTT-27): Xác nhận đơn và chuyển sang giao hàng.
    // -----------------------------------------------------------------------

    public async Task<Result<OrderDetailDto>> ConfirmOrderAsync(
        long orderId,
        ConfirmOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new NotFoundException("Không tìm thấy đơn hàng.", "ORDER_NOT_FOUND");
        }

        order.Confirm(request.Note);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDetailDto(order));
    }

    public async Task<Result<OrderDetailDto>> ShipOrderAsync(
        long orderId,
        ShipOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new NotFoundException("Không tìm thấy đơn hàng.", "ORDER_NOT_FOUND");
        }

        order.Ship(request.Carrier, request.TrackingNumber, request.Note);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDetailDto(order));
    }

    // -----------------------------------------------------------------------
    // FR-11 (N2LHTBLTT-28): Giao thành công (thu COD) và giao thất bại.
    // -----------------------------------------------------------------------

    public async Task<Result<OrderDetailDto>> DeliverOrderAsync(
        long orderId,
        DeliverOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new NotFoundException("Không tìm thấy đơn hàng.", "ORDER_NOT_FOUND");
        }

        order.Deliver(request.CollectedAmount, request.Note);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDetailDto(order));
    }

    public async Task<Result<OrderDetailDto>> FailDeliveryAsync(
        long orderId,
        DeliveryFailedRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            throw new NotFoundException("Không tìm thấy đơn hàng.", "ORDER_NOT_FOUND");
        }

        order.MarkDeliveryFailed(request.Reason, request.CancelOrder);
        await _orderRepository.SaveChangesAsync(cancellationToken);

        // Nếu đơn bị huỷ do giao thất bại thì phải hoàn tồn kho.
        await _domainEventDispatcher.DispatchAsync(order.DomainEvents, cancellationToken);
        order.ClearDomainEvents();

        return Result.Success(ToDetailDto(order));
    }

    public async Task<Result<PagedResult<CodCollectionDto>>> GetCodCollectionsAsync(
        OrderFilterQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _orderRepository.QueryDelivered();

        if (query.FromDate.HasValue)
        {
            var fromDate = query.FromDate.Value;
            baseQuery = baseQuery.Where(order => order.DeliveredAt >= fromDate);
        }

        if (query.ToDate.HasValue)
        {
            var toDate = query.ToDate.Value;
            baseQuery = baseQuery.Where(order => order.DeliveredAt <= toDate);
        }

        var totalCount = await baseQuery.LongCountAsync(cancellationToken);

        var orders = await baseQuery
            .OrderByDescending(order => order.DeliveredAt)
            .ThenByDescending(order => order.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = orders
            .Select(order => new CodCollectionDto(
                order.Id,
                order.OrderNumber,
                order.TotalAmount,
                order.CollectedAmount ?? order.TotalAmount,
                order.CollectedAt ?? order.DeliveredAt ?? order.CreatedAt))
            .ToList();

        return Result.Success(PagedResult<CodCollectionDto>.Create(dtos, page, pageSize, totalCount));
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
            order.Carrier,
            order.TrackingNumber,
            order.CollectedAmount,
            order.CollectedAt,
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
            order.Carrier,
            order.TrackingNumber,
            order.CollectedAmount,
            order.CollectedAt,
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
