using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// FR-10 (N2LHTBLTT-27) / FR-11 (N2LHTBLTT-28): Kiểm thử tích hợp luồng
/// xác nhận → giao hàng → giao thành công / thất bại và thu tiền COD.
///
/// Phủ:
/// - Nhân viên xác nhận và bàn giao đơn hàng.
/// - Giao thành công phải thu đủ số tiền COD, thiếu thì bị từ chối.
/// - Giao thất bại thì chuyển sang DELIVERY_FAILED, hoặc huỷ đơn (hoàn tồn kho).
/// - Khách hàng không được gọi các endpoint của nhân viên.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class OrderFulfillmentApiTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _staffClient = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public OrderFulfillmentApiTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _staffClient = await AuthTestHelper.CreateAuthenticatedStaffAsync(_factory);
    }

    public Task DisposeAsync()
    {
        _staffClient.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Staff_can_confirm_ship_and_deliver_an_order()
    {
        var order = await CreateOrderAsync();

        var confirmResponse = await _staffClient.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/confirm",
            new { note = "Xác nhận đơn" },
            Json);

        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        confirmed.GetProperty("data").GetProperty("status").GetString().Should().Be("CONFIRMED");

        var shipResponse = await _staffClient.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/ship",
            new { carrier = "Giao Hàng Nhanh", trackingNumber = "GHN999", note = "Bàn giao" },
            Json);

        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var shipped = await shipResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var shippedData = shipped.GetProperty("data");
        shippedData.GetProperty("status").GetString().Should().Be("SHIPPING");
        shippedData.GetProperty("carrier").GetString().Should().Be("Giao Hàng Nhanh");
        shippedData.GetProperty("trackingNumber").GetString().Should().Be("GHN999");

        var deliverResponse = await _staffClient.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/deliver",
            new { collectedAmount = order.TotalAmount, note = "Đã thu tiền" },
            Json);

        deliverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var delivered = await deliverResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var deliveredData = delivered.GetProperty("data");
        deliveredData.GetProperty("status").GetString().Should().Be("DELIVERED");
        deliveredData.GetProperty("collectedAmount").GetInt64().Should().Be(order.TotalAmount);
        deliveredData.GetProperty("deliveredAt").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Order_status_history_records_each_transition()
    {
        var order = await CreateOrderAsync();

        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/confirm", new { note = (string?)null }, Json);
        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/ship", new { carrier = (string?)null, trackingNumber = (string?)null, note = (string?)null }, Json);
        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/deliver", new { collectedAmount = order.TotalAmount, note = (string?)null }, Json);

        var detail = await _staffClient.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{order.Id}", Json);
        var histories = detail.GetProperty("data").GetProperty("statusHistories");

        histories.GetArrayLength().Should().BeGreaterThanOrEqualTo(4);
        histories[0].GetProperty("toStatus").GetString().Should().Be("PENDING");
        histories[histories.GetArrayLength() - 1].GetProperty("toStatus").GetString().Should().Be("DELIVERED");
    }

    [Fact]
    public async Task Delivering_with_insufficient_cod_amount_is_rejected()
    {
        var order = await CreateOrderAsync();

        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/confirm", new { note = (string?)null }, Json);
        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/ship", new { carrier = (string?)null, trackingNumber = (string?)null, note = (string?)null }, Json);

        var response = await _staffClient.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/deliver",
            new { collectedAmount = order.TotalAmount - 1, note = (string?)null },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Confirming_a_shipped_order_is_rejected()
    {
        var order = await CreateOrderAsync();

        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/confirm", new { note = (string?)null }, Json);
        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/ship", new { carrier = (string?)null, trackingNumber = (string?)null, note = (string?)null }, Json);

        var response = await _staffClient.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/confirm",
            new { note = (string?)null },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("INVALID_ORDER_STATUS");
    }

    [Fact]
    public async Task Failed_delivery_without_cancel_moves_order_to_delivery_failed()
    {
        var order = await CreateOrderAsync();

        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/confirm", new { note = (string?)null }, Json);
        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/ship", new { carrier = (string?)null, trackingNumber = (string?)null, note = (string?)null }, Json);

        var response = await _staffClient.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/delivery-failed",
            new { reason = "Khách không nhận hàng", cancelOrder = false },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("data").GetProperty("status").GetString().Should().Be("DELIVERY_FAILED");
    }

    [Fact]
    public async Task Failed_delivery_with_cancel_cancels_the_order()
    {
        var order = await CreateOrderAsync();

        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/confirm", new { note = (string?)null }, Json);
        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/ship", new { carrier = (string?)null, trackingNumber = (string?)null, note = (string?)null }, Json);

        var response = await _staffClient.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/delivery-failed",
            new { reason = "Khách từ chối nhận", cancelOrder = true },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");
        data.GetProperty("status").GetString().Should().Be("CANCELLED");
        data.GetProperty("cancelReason").GetString().Should().Be("Khách từ chối nhận");
    }

    [Fact]
    public async Task Customer_cannot_confirm_an_order()
    {
        var order = await CreateOrderAsync();

        using var customerClient = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);

        var response = await customerClient.PostAsJsonAsync(
            $"/api/v1/orders/{order.Id}/confirm",
            new { note = (string?)null },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cod_collections_list_contains_delivered_orders()
    {
        var order = await CreateOrderAsync();

        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/confirm", new { note = (string?)null }, Json);
        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/ship", new { carrier = (string?)null, trackingNumber = (string?)null, note = (string?)null }, Json);
        await _staffClient.PostAsJsonAsync($"/api/v1/orders/{order.Id}/deliver", new { collectedAmount = order.TotalAmount, note = (string?)null }, Json);

        var response = await _staffClient.GetAsync("/api/v1/admin/orders/cod-collections?page=1&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items");

        items.GetArrayLength().Should().BeGreaterThan(0);

        var matched = items.EnumerateArray()
            .FirstOrDefault(item => item.GetProperty("orderId").GetInt64() == order.Id);

        matched.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined);
        matched.GetProperty("collectedAmount").GetInt64().Should().Be(order.TotalAmount);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private async Task<(long Id, long TotalAmount)> CreateOrderAsync()
    {
        using var customerClient = await AuthTestHelper.CreateAuthenticatedCustomerAsync(_factory);

        var products = await customerClient.GetFromJsonAsync<JsonElement>("/api/v1/products", Json);
        var productId = products.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();

        var detail = await customerClient.GetFromJsonAsync<JsonElement>($"/api/v1/products/{productId}", Json);
        var variantId = detail.GetProperty("data").GetProperty("variants")[0].GetProperty("id").GetInt64();

        var addResponse = await customerClient.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            Json);

        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var checkoutResponse = await customerClient.PostAsJsonAsync(
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn A",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ, Hà Nội",
                note = (string?)null
            },
            Json);

        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await checkoutResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        return (data.GetProperty("id").GetInt64(), data.GetProperty("totalAmount").GetInt64());
    }
}
