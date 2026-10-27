using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-39: Kiểm thử tích hợp end-to-end cho luồng đặt hàng COD.
///
/// Bao gồm:
/// - Luồng đầy đủ: giỏ → checkout → chi tiết đơn → hủy → hoàn tồn kho.
/// - Tính đúng tổng tiền, phí vận chuyển và phạm vi giá.
/// - Tính nhất quán dữ liệu giữa các module (cart, ordering, inventory).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class EndToEndOrderJourneyTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static int _userIdCounter = 3000;

    public EndToEndOrderJourneyTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // N2LHTBLTT-8 / N2LHTBLTT-9: xac thuc bang JWT that.
        _client = await ApiTestHelper.CreateUserClient(_factory, 0);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Full_journey_from_browsing_to_cancelling_the_order()
    {
        // 1. Khách xem danh mục.
        var products = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products");
        products.GetProperty("items").EnumerateArray().Should().NotBeEmpty();

        // 2. Chọn sản phẩm và xem chi tiết.
        var productId = products.GetProperty("items")[0].GetProperty("id").GetInt64();
        var detail = await ApiTestHelper.GetDataAsync(_client, $"/api/v1/products/{productId}");
        var variant = detail.GetProperty("variants")[0];
        var variantId = variant.GetProperty("id").GetInt64();
        var price = variant.GetProperty("price").GetInt64();

        // 3. Thêm vào giỏ.
        var addResponse = await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 2 },
            ApiTestHelper.Json);
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        cart.GetProperty("totalQuantity").GetInt32().Should().Be(2);
        cart.GetProperty("subtotal").GetInt64().Should().Be(price * 2);

        // 4. Checkout COD.
        var (checkoutStatus, checkoutBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ, Quận 1, TP.HCM",
                note = "Giao sau 18h"
            });

        checkoutStatus.Should().Be(HttpStatusCode.Created);

        var order = checkoutBody.GetProperty("data");
        var orderId = order.GetProperty("id").GetInt64();

        order.GetProperty("status").GetString().Should().Be("PENDING");
        order.GetProperty("paymentMethod").GetString().Should().Be("COD");
        order.GetProperty("subtotal").GetInt64().Should().Be(price * 2);
        order.GetProperty("totalAmount").GetInt64().Should().Be(price * 2 + order.GetProperty("shippingFee").GetInt64());
        order.GetProperty("note").GetString().Should().Be("Giao sau 18h");
        order.GetProperty("items").GetArrayLength().Should().Be(1);

        // 5. Đơn xuất hiện trong danh sách và xem được chi tiết.
        var list = await ApiTestHelper.GetDataAsync(_client, "/api/v1/orders");
        list.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt64() == orderId);

        var orderDetail = await ApiTestHelper.GetDataAsync(_client, $"/api/v1/orders/{orderId}");
        orderDetail.GetProperty("orderNumber").GetString().Should().Be(order.GetProperty("orderNumber").GetString());
        orderDetail.GetProperty("statusHistories").GetArrayLength().Should().BeGreaterThan(0);

        // 6. Hủy đơn.
        var (cancelStatus, cancelBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Đổi ý, không mua nữa." });

        cancelStatus.Should().Be(HttpStatusCode.OK);

        var cancelled = cancelBody.GetProperty("data");
        cancelled.GetProperty("status").GetString().Should().Be("CANCELLED");
        cancelled.GetProperty("cancelReason").GetString().Should().Be("Đổi ý, không mua nữa.");

        // 7. Giỏ hàng được tạo mới và trống.
        var newCart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        newCart.GetProperty("totalQuantity").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Order_creation_persists_the_items_in_the_database()
    {
        var (variantId, price, sku) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var order = await ApiTestHelper.CheckoutAsync(_client, variantId, quantity: 2);
        var orderId = order.GetProperty("id").GetInt64();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PhoneStore.Modules.Order.Infrastructure.Persistence.OrderDbContext>();

        var persisted = await dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        persisted.Should().NotBeNull();
        var persistedItems = persisted!.Items.ToList();
        persistedItems.Should().HaveCount(1);
        persistedItems[0].ProductVariantId.Should().Be(variantId);
        persistedItems[0].Quantity.Should().Be(2);
        persistedItems[0].UnitPrice.Should().Be(price);
        persistedItems[0].Sku.Should().Be(sku);
        persisted.TotalAmount.Should().Be(price * 2 + persisted.ShippingFee);
    }

    [Fact]
    public async Task Order_status_history_records_the_full_lifecycle()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);
        var order = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var orderId = order.GetProperty("id").GetInt64();

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Kiểm tra lịch sử." });

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PhoneStore.Modules.Order.Infrastructure.Persistence.OrderDbContext>();

        var histories = await dbContext.OrderStatusHistories
            .AsNoTracking()
            .Where(h => h.OrderId == orderId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        histories.Should().HaveCountGreaterThanOrEqualTo(2);

        histories[0].FromStatus.Should().BeNull();
        histories[0].ToStatus.Should().Be(PhoneStore.Modules.Order.Domain.Enums.OrderStatus.PENDING);

        histories[^1].FromStatus.Should().Be(PhoneStore.Modules.Order.Domain.Enums.OrderStatus.PENDING);
        histories[^1].ToStatus.Should().Be(PhoneStore.Modules.Order.Domain.Enums.OrderStatus.CANCELLED);
    }

    [Fact]
    public async Task Cancelling_an_order_restores_the_stock_of_the_ordered_variant()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var stockBefore = await GetStockAsync(variantId);

        var order = await ApiTestHelper.CheckoutAsync(_client, variantId, quantity: 2);
        var orderId = order.GetProperty("id").GetInt64();

        // Đơn hàng chưa trừ kho (xem ghi chú trong PR #1), nên tồn kho ở thời điểm
        // này vẫn bằng giá trị ban đầu. Test này chốt lại hành vi hiện tại để khi
        // Inventory được nối vào checkout, thay đổi đó sẽ lộ ra ngay.
        var stockAfterCheckout = await GetStockAsync(variantId);
        stockAfterCheckout.Should().Be(stockBefore);

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Kiểm thử hoàn tồn." });

        var stockAfterCancel = await GetStockAsync(variantId);

        // Hủy đơn hoàn lại đúng số lượng đã đặt.
        stockAfterCancel.Should().Be(stockBefore + 2);
    }

    [Fact]
    public async Task Cancelling_an_order_twice_does_not_restore_stock_twice()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var stockBefore = await GetStockAsync(variantId);

        var order = await ApiTestHelper.CheckoutAsync(_client, variantId, quantity: 2);
        var orderId = order.GetProperty("id").GetInt64();

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Hủy lần 1." });

        var (secondStatus, _) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Hủy lần 2." });

        secondStatus.Should().Be(HttpStatusCode.Conflict);

        var stockAfter = await GetStockAsync(variantId);
        stockAfter.Should().Be(stockBefore + 2);
    }

    [Fact]
    public async Task Cancelled_cart_is_not_reused_for_the_next_order()
    {
        var (variantId, price, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var firstCart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        var firstCartId = firstCart.GetProperty("id").GetInt64();

        var firstOrder = await ApiTestHelper.CheckoutAsync(_client, variantId, quantity: 1);
        firstOrder.GetProperty("subtotal").GetInt64().Should().Be(price);

        var secondCart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        var secondCartId = secondCart.GetProperty("id").GetInt64();

        // Giỏ mới phải là giỏ khác giỏ cũ đã CONVERTED.
        secondCartId.Should().NotBe(firstCartId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PhoneStore.Modules.Cart.Infrastructure.Persistence.CartDbContext>();

        var oldCart = await dbContext.Carts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == firstCartId);

        oldCart.Should().NotBeNull();
        oldCart!.Status.Should().Be(PhoneStore.Modules.Cart.Domain.Enums.CartStatus.CONVERTED);
    }

    [Fact]
    public async Task Checkout_with_a_note_longer_than_500_characters_is_rejected()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            ApiTestHelper.Json);

        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ",
                note = new string('a', 501)
            });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Checkout_rejects_a_missing_shipping_address()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            ApiTestHelper.Json);

        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = ""
            });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Checkout_accepts_an_international_phone_number_format()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            ApiTestHelper.Json);

        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "+84912345678",
                shippingAddress = "123 Nguyễn Huệ"
            });

        status.Should().Be(HttpStatusCode.Created);
        body.GetProperty("data").GetProperty("receiverPhone").GetString().Should().Be("+84912345678");
    }

    [Fact]
    public async Task Order_history_is_listed_newest_first()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var first = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var second = await ApiTestHelper.CheckoutAsync(_client, variantId);

        var list = await ApiTestHelper.GetDataAsync(_client, "/api/v1/orders");
        var ids = list.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64())
            .ToList();

        var firstId = first.GetProperty("id").GetInt64();
        var secondId = second.GetProperty("id").GetInt64();

        ids.Should().Contain(firstId);
        ids.Should().Contain(secondId);
        ids.IndexOf(secondId).Should().BeLessThan(ids.IndexOf(firstId));
    }

    [Fact]
    public async Task Filtering_orders_by_date_range_returns_the_order_created_in_that_range()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);
        var order = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var orderId = order.GetProperty("id").GetInt64();

        var fromDate = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var toDate = DateTimeOffset.UtcNow.AddDays(1).ToString("O");

        var filtered = await ApiTestHelper.GetDataAsync(
            _client,
            $"/api/v1/orders?fromDate={Uri.EscapeDataString(fromDate)}&toDate={Uri.EscapeDataString(toDate)}");

        filtered.GetProperty("items").EnumerateArray()
            .Should().Contain(item => item.GetProperty("id").GetInt64() == orderId);
    }

    [Fact]
    public async Task Filtering_orders_by_a_past_date_range_excludes_new_orders()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);
        var order = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var orderId = order.GetProperty("id").GetInt64();

        var fromDate = DateTimeOffset.UtcNow.AddDays(-30).ToString("O");
        var toDate = DateTimeOffset.UtcNow.AddDays(-29).ToString("O");

        var filtered = await ApiTestHelper.GetDataAsync(
            _client,
            $"/api/v1/orders?fromDate={Uri.EscapeDataString(fromDate)}&toDate={Uri.EscapeDataString(toDate)}");

        filtered.GetProperty("items").EnumerateArray()
            .Should().NotContain(item => item.GetProperty("id").GetInt64() == orderId);
    }

    [Fact]
    public async Task Order_list_supports_pagination()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await ApiTestHelper.CheckoutAsync(_client, variantId);
        await ApiTestHelper.CheckoutAsync(_client, variantId);

        var page1 = await ApiTestHelper.GetDataAsync(_client, "/api/v1/orders?page=1&pageSize=1");
        var page2 = await ApiTestHelper.GetDataAsync(_client, "/api/v1/orders?page=2&pageSize=1");

        page1.GetProperty("page").GetInt32().Should().Be(1);
        page2.GetProperty("page").GetInt32().Should().Be(2);

        page1.GetProperty("items").GetArrayLength().Should().Be(1);
        page2.GetProperty("items").GetArrayLength().Should().Be(1);

        var page1Id = page1.GetProperty("items")[0].GetProperty("id").GetInt64();
        var page2Id = page2.GetProperty("items")[0].GetProperty("id").GetInt64();

        page1Id.Should().NotBe(page2Id);
    }

    [Fact]
    public async Task Order_detail_of_an_unknown_order_returns_not_found()
    {
        var (status, body) = await ApiTestHelper.SendAsync(_client, HttpMethod.Get, "/api/v1/orders/999999999");

        status.Should().Be(HttpStatusCode.NotFound);
        ApiTestHelper.AssertErrorEnvelope(body, "ORDER_NOT_FOUND");
    }

    [Fact]
    public async Task Cart_item_quantity_survives_a_round_trip_through_the_database()
    {
        var (variantId, price, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 3 },
            ApiTestHelper.Json);

        var cart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        var cartItemId = cart.GetProperty("items")[0].GetProperty("id").GetInt64();

        var (updateStatus, updateBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Put,
            $"/api/v1/cart/items/{cartItemId}",
            new { quantity = 7 });

        updateStatus.Should().Be(HttpStatusCode.OK);

        var updated = updateBody.GetProperty("data");
        updated.GetProperty("totalQuantity").GetInt32().Should().Be(7);
        updated.GetProperty("subtotal").GetInt64().Should().Be(price * 7);

        // Đọc lại từ database để chắc chắn đã persist.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PhoneStore.Modules.Cart.Infrastructure.Persistence.CartDbContext>();

        var persisted = await dbContext.CartItems.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == cartItemId);

        persisted.Should().NotBeNull();
        persisted!.Quantity.Should().Be(7);
        persisted.UnitPrice.Should().Be(price);
    }

    [Fact]
    public async Task Removing_the_only_cart_item_leaves_an_empty_cart_that_cannot_be_checked_out()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            ApiTestHelper.Json);

        var cart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        var cartItemId = cart.GetProperty("items")[0].GetProperty("id").GetInt64();

        var (removeStatus, removeBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Delete,
            $"/api/v1/cart/items/{cartItemId}");

        removeStatus.Should().Be(HttpStatusCode.OK);
        removeBody.GetProperty("data").GetProperty("items").GetArrayLength().Should().Be(0);

        var (checkoutStatus, _) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ"
            });

        checkoutStatus.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<int> GetStockAsync(long productVariantId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<PhoneStore.Modules.Inventory.Infrastructure.Persistence.InventoryDbContext>();

        return await dbContext.ProductStocks
            .AsNoTracking()
            .Where(item => item.ProductVariantId == productVariantId)
            .Select(item => item.StockQuantity)
            .FirstOrDefaultAsync();
    }
}
