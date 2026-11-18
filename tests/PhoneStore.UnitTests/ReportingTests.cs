using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PhoneStore.BuildingBlocks.Contracts.Inventory;
using PhoneStore.Modules.Order.Application.Services;
using PhoneStore.Modules.Order.Domain.Entities;
using PhoneStore.Modules.Order.Domain.Enums;
using PhoneStore.Modules.Order.Infrastructure.Persistence;
using Xunit;

namespace PhoneStore.UnitTests;

/// <summary>
/// FR-16 (N2LHTBLTT-32 / TC-18): Bộ kiểm thử đơn vị cho API báo cáo thống kê doanh thu,
/// trạng thái đơn hàng và tồn kho thấp cho Quản trị.
/// </summary>
public class ReportingTests
{
    private class FakeInventoryContract : IInventoryContract
    {
        public List<LowStockItemDto> LowStockResult { get; set; } = [];

        public Task<bool> TryReserveStockAsync(IReadOnlyCollection<StockChangeItem> items, long? orderId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task RestoreStockAsync(IReadOnlyCollection<StockChangeItem> items, long? orderId = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<LowStockItemDto>> GetLowStockAsync(int threshold = 10, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LowStockItemDto>>(LowStockResult);
    }

    private OrderDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new OrderDbContext(options);
    }

    [Fact]
    public async Task GetOrderStatusReport_ShouldReturnCountsForAllStatuses()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var fakeInventory = new FakeInventoryContract();

        // Tạo 2 đơn PENDING, 1 CONFIRMED, 1 DELIVERED, 1 CANCELLED
        var draft = new[] { new OrderItemDraft(1, "iPhone 15", "IP15-128", 20_000_000, 1) };
        var order1 = Order.Create("ORD-001", 101, "A", "0900000001", "Hanoi", null, draft);
        var order2 = Order.Create("ORD-002", 102, "B", "0900000002", "Hanoi", null, draft);

        var order3 = Order.Create("ORD-003", 103, "C", "0900000003", "Hanoi", null, draft);
        order3.ChangeStatus(OrderStatus.CONFIRMED);

        var order4 = Order.Create("ORD-004", 104, "D", "0900000004", "Hanoi", null, draft);
        order4.ChangeStatus(OrderStatus.CONFIRMED);
        order4.ChangeStatus(OrderStatus.SHIPPING);
        order4.ChangeStatus(OrderStatus.DELIVERED);

        var order5 = Order.Create("ORD-005", 105, "E", "0900000005", "Hanoi", null, draft);
        order5.ChangeStatus(OrderStatus.CANCELLED, "Khách đổi ý");

        dbContext.Orders.AddRange(order1, order2, order3, order4, order5);
        await dbContext.SaveChangesAsync();

        var service = new ReportingService(dbContext, fakeInventory);

        // Act
        var result = await service.GetOrderStatusReportAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var report = result.Value;
        report.TotalOrders.Should().Be(5);
        report.StatusCounts[OrderStatus.PENDING.ToString()].Should().Be(2);
        report.StatusCounts[OrderStatus.CONFIRMED.ToString()].Should().Be(1);
        report.StatusCounts[OrderStatus.SHIPPING.ToString()].Should().Be(0);
        report.StatusCounts[OrderStatus.DELIVERED.ToString()].Should().Be(1);
        report.StatusCounts[OrderStatus.CANCELLED.ToString()].Should().Be(1);
    }

    [Fact]
    public async Task GetRevenueReport_OnlyDeliveredOrders_ShouldBeCountedInRevenue()
    {
        // Arrange (TC-18: Chỉ DELIVERED được tính doanh thu, phí giao hiển thị riêng)
        using var dbContext = CreateInMemoryDbContext();
        var fakeInventory = new FakeInventoryContract();

        var draft1 = new[] { new OrderItemDraft(1, "iPhone 15", "IP15-128", 20_000_000, 1) };
        var draft2 = new[] { new OrderItemDraft(2, "iPhone 15 Pro", "IP15P-128", 25_000_000, 1) };
        var draft3 = new[] { new OrderItemDraft(3, "Phụ kiện", "PK-01", 500_000, 1) }; // Có phí ship 30.000

        // Đơn 1: DELIVERED (Subtotal: 20tr, Ship: 0, Total: 20tr)
        var order1 = Order.Create("ORD-001", 101, "A", "0900000001", "Hanoi", null, draft1);
        order1.ChangeStatus(OrderStatus.CONFIRMED);
        order1.ChangeStatus(OrderStatus.SHIPPING);
        order1.ChangeStatus(OrderStatus.DELIVERED);

        // Đơn 2: DELIVERED (Subtotal: 500k, Ship: 30k, Total: 530k)
        var order2 = Order.Create("ORD-002", 102, "B", "0900000002", "Hanoi", null, draft3);
        order2.ChangeStatus(OrderStatus.CONFIRMED);
        order2.ChangeStatus(OrderStatus.SHIPPING);
        order2.ChangeStatus(OrderStatus.DELIVERED);

        // Đơn 3: SHIPPING (Đang giao, KHÔNG được tính doanh thu)
        var order3 = Order.Create("ORD-003", 103, "C", "0900000003", "Hanoi", null, draft2);
        order3.ChangeStatus(OrderStatus.CONFIRMED);
        order3.ChangeStatus(OrderStatus.SHIPPING);

        // Đơn 4: CANCELLED (Đã hủy, KHÔNG được tính doanh thu)
        var order4 = Order.Create("ORD-004", 104, "D", "0900000004", "Hanoi", null, draft2);
        order4.ChangeStatus(OrderStatus.CANCELLED);

        dbContext.Orders.AddRange(order1, order2, order3, order4);
        await dbContext.SaveChangesAsync();

        var service = new ReportingService(dbContext, fakeInventory);

        // Act
        var result = await service.GetRevenueReportAsync(null, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var rev = result.Value;
        rev.DeliveredOrderCount.Should().Be(2);
        rev.TotalRevenue.Should().Be(20_500_000); // 20tr + 500k
        rev.TotalShippingFee.Should().Be(30_000); // Phí giao hiển thị riêng
    }

    [Fact]
    public async Task GetDashboardReport_ShouldCombineOrderCountsRevenueAndLowStock()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var fakeInventory = new FakeInventoryContract
        {
            LowStockResult =
            [
                new(10, 2),
                new(11, 0)
            ]
        };

        var draft = new[] { new OrderItemDraft(1, "iPhone 15", "IP15-128", 20_000_000, 1) };
        var order = Order.Create("ORD-001", 101, "A", "0900000001", "Hanoi", null, draft);
        order.ChangeStatus(OrderStatus.CONFIRMED);
        order.ChangeStatus(OrderStatus.SHIPPING);
        order.ChangeStatus(OrderStatus.DELIVERED);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var service = new ReportingService(dbContext, fakeInventory);

        // Act
        var result = await service.GetDashboardReportAsync(null, null, lowStockThreshold: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var dashboard = result.Value;
        dashboard.TotalOrders.Should().Be(1);
        dashboard.Revenue.DeliveredOrderCount.Should().Be(1);
        dashboard.Revenue.TotalRevenue.Should().Be(20_000_000);
        dashboard.LowStockItems.Should().HaveCount(2);
        dashboard.LowStockItems[0].ProductVariantId.Should().Be(10);
        dashboard.LowStockItems[0].StockQuantity.Should().Be(2);
    }
}
