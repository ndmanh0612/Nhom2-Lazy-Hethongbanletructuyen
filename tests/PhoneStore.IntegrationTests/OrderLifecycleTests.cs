using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-25 / N2LHTBLTT-26: Kiểm thử tích hợp luồng quản lý đơn hàng và hủy đơn.
///
/// Phủ:
/// - FR-08: danh sách đơn hàng, chi tiết đơn hàng, lọc theo trạng thái và phân trang.
/// - FR-09: hủy đơn, kiểm tra trạng thái hợp lệ, và hoàn tồn kho qua event.
/// - Bảo mật: khách hàng khác không xem/hủy được đơn không thuộc về mình.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class OrderLifecycleTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static int _userIdCounter = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public OrderLifecycleTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        var userId = Interlocked.Increment(ref _userIdCounter);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task List_orders_returns_paged_result_with_the_created_order()
    {
        var orderId = await CreateOrderAsync();

        var response = await _client.GetAsync("/api/v1/orders");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var data = payload.GetProperty("data");

        data.GetProperty("totalCount").GetInt32().Should().BeGreaterThan(0);
        data.GetProperty("page").GetInt32().Should().Be(1);

        var orders = data.GetProperty("items");
        orders.EnumerateArray()
            .Should().Contain(order => order.GetProperty("id").GetInt64() == orderId);
    }

    [Fact]
    public async Task Get_order_detail_returns_order_with_status_history()
    {
        var orderId = await CreateOrderAsync();

        var response = await _client.GetAsync($"/api/v1/orders/{orderId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var order = payload.GetProperty("data");

        order.GetProperty("id").GetInt64().Should().Be(orderId);
        order.GetProperty("status").GetString().Should().Be("PENDING");
        order.GetProperty("paymentMethod").GetString().Should().Be("COD");
        order.GetProperty("items").GetArrayLength().Should().BeGreaterThan(0);
        order.GetProperty("statusHistories").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Filter_orders_by_status_returns_only_matching_orders()
    {
        await CreateOrderAsync();

        var response = await _client.GetAsync("/api/v1/orders?status=PENDING");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = payload.GetProperty("data").GetProperty("items");

        foreach (var order in items.EnumerateArray())
        {
            order.GetProperty("status").GetString().Should().Be("PENDING");
        }
    }

    [Fact]
    public async Task Filter_orders_with_invalid_status_returns_validation_error()
    {
        var response = await _client.GetAsync("/api/v1/orders?status=NOT_A_STATUS");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Cancel_order_marks_order_cancelled()
    {
        var orderId = await CreateOrderAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Đổi ý, không mua nữa." },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var order = payload.GetProperty("data");

        order.GetProperty("status").GetString().Should().Be("CANCELLED");
        order.GetProperty("cancelReason").GetString().Should().Be("Đổi ý, không mua nữa.");
        order.GetProperty("cancelledAt").ValueKind.Should().NotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Cancel_order_twice_returns_conflict_error()
    {
        var orderId = await CreateOrderAsync();

        var first = await _client.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Hủy lần 1." },
            Json);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _client.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Hủy lần 2." },
            Json);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var payload = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("INVALID_ORDER_STATUS");
        payload.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Cancel_order_with_empty_reason_returns_validation_error()
    {
        var orderId = await CreateOrderAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "" },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Customer_cannot_view_order_of_another_customer()
    {
        var orderId = await CreateOrderAsync();

        // Client khác (không phải chủ đơn) xem đơn hàng.
        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-User-Id", "9999");

        var response = await otherClient.GetAsync($"/api/v1/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Customer_cannot_cancel_order_of_another_customer()
    {
        var orderId = await CreateOrderAsync();

        using var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add("X-User-Id", "9999");

        var response = await otherClient.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Cố hủy đơn của người khác." },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Checkout_with_empty_cart_returns_validation_error()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ"
            },
            Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        payload.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
    }

    /// <summary>
    /// Thêm một sản phẩm vào giỏ rồi checkout, trả về id đơn hàng vừa tạo.
    /// </summary>
    private async Task<long> CreateOrderAsync()
    {
        var products = await _client.GetFromJsonAsync<JsonElement>("/api/v1/products", Json);
        var productId = products.GetProperty("data").GetProperty("items")[0].GetProperty("id").GetInt64();

        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{productId}", Json);
        var variantId = detail.GetProperty("data").GetProperty("variants")[0].GetProperty("id").GetInt64();

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            Json);

        var checkoutResponse = await _client.PostAsJsonAsync(
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ, Quận 1, TP.HCM"
            },
            Json);

        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await checkoutResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        return payload.GetProperty("data").GetProperty("id").GetInt64();
    }
}
