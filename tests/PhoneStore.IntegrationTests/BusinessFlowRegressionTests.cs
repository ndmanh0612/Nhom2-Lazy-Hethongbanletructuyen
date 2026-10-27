using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PhoneStore.IntegrationTests;

/// <summary>
/// N2LHTBLTT-39: Regression test cho các luồng nghiệp vụ quan trọng.
///
/// Mỗi test trong file này chốt lại một hành vi đã được xác nhận là đúng, để khi
/// các module khác (Identity, Inventory, Order) được nối vào thì những thay đổi đó
/// không vô tình phá vỡ luồng hiện tại.
///
/// Nhóm test được viết theo dạng "hộp đen" — chỉ gọi HTTP API và kiểm tra
/// status code + payload trả về, để phản ánh đúng hành vi mà client thấy.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class BusinessFlowRegressionTests : IAsyncLifetime
{
    private readonly PhoneStoreApiFactory _factory;
    private HttpClient _client = null!;

    private static int _userIdCounter = 2000;

    public BusinessFlowRegressionTests(PhoneStoreApiFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync()
    {
        var userId = Interlocked.Increment(ref _userIdCounter);
        _client = ApiTestHelper.CreateUserClient(_factory, userId);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------------
    // Giỏ hàng (FR-06)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Cart_is_isolated_per_customer()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 2 },
            ApiTestHelper.Json);

        var myCart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        myCart.GetProperty("totalQuantity").GetInt32().Should().Be(2);

        // Khách khác không được thấy giỏ của khách này.
        using var otherClient = ApiTestHelper.CreateUserClient(_factory, 999_001);
        var otherCart = await ApiTestHelper.GetDataAsync(otherClient, "/api/v1/cart");

        otherCart.GetProperty("totalQuantity").GetInt32().Should().Be(0);
        otherCart.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Cart_items_from_two_customers_coexist_in_the_same_database()
    {
        var (variantId, price, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        using var otherClient = ApiTestHelper.CreateUserClient(_factory, 999_002);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            ApiTestHelper.Json);

        await otherClient.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 3 },
            ApiTestHelper.Json);

        var myCart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        var otherCart = await ApiTestHelper.GetDataAsync(otherClient, "/api/v1/cart");

        myCart.GetProperty("totalQuantity").GetInt32().Should().Be(1);
        myCart.GetProperty("subtotal").GetInt64().Should().Be(price);

        otherCart.GetProperty("totalQuantity").GetInt32().Should().Be(3);
        otherCart.GetProperty("subtotal").GetInt64().Should().Be(price * 3);
    }

    [Fact]
    public async Task Adding_more_than_max_quantity_to_the_same_variant_is_rejected_with_validation_error()
    {
        // Regression: thêm 6 rồi thêm 6 lần nữa (tổng 12 > MaxQuantityPerItem = 10)
        // từng làm API trả 500 INTERNAL_SERVER_ERROR vì domain ném ArgumentException
        // chưa được map về AppException. Nay phải là 400 VALIDATION_ERROR.
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var first = await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 6 },
            ApiTestHelper.Json);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 6 });

        status.Should().Be(
            HttpStatusCode.BadRequest,
            "Vượt quá số lượng tối đa mỗi sản phẩm phải trả 400 chứ không phải 500.");

        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Cart_quantity_stays_within_the_limit_after_rejected_merge()
    {
        // Sau khi request vượt giới hạn bị từ chối, giỏ phải giữ nguyên số lượng cũ.
        var (variantId, price, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 6 },
            ApiTestHelper.Json);

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 6 });

        var cart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        cart.GetProperty("totalQuantity").GetInt32().Should().Be(6);
        cart.GetProperty("subtotal").GetInt64().Should().Be(price * 6);
    }

    // ---------------------------------------------------------------------
    // Checkout (FR-07)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Checkout_clears_the_cart_and_creates_a_new_active_cart_for_the_next_purchase()
    {
        var (variantId, price, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var order = await ApiTestHelper.CheckoutAsync(_client, variantId, quantity: 2);

        order.GetProperty("subtotal").GetInt64().Should().Be(price * 2);
        order.GetProperty("totalAmount").GetInt64().Should().Be(price * 2);

        // Giỏ cũ đã chuyển thành CONVERTED, GET /cart trả về giỏ ACTIVE mới và rỗng.
        var cart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        cart.GetProperty("status").GetString().Should().Be("ACTIVE");
        cart.GetProperty("totalQuantity").GetInt32().Should().Be(0);
        cart.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Checkout_twice_in_a_row_fails_because_the_cart_is_already_converted()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await ApiTestHelper.CheckoutAsync(_client, variantId);

        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/orders/checkout",
            new
            {
                receiverName = "Nguyễn Văn Khách",
                receiverPhone = "0912345678",
                shippingAddress = "123 Nguyễn Huệ"
            });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Each_order_gets_a_unique_order_number()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var first = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var second = await ApiTestHelper.CheckoutAsync(_client, variantId);

        var firstNumber = first.GetProperty("orderNumber").GetString();
        var secondNumber = second.GetProperty("orderNumber").GetString();

        firstNumber.Should().NotBeNullOrWhiteSpace();
        secondNumber.Should().NotBeNullOrWhiteSpace();
        firstNumber.Should().NotBe(secondNumber);
    }

    [Fact]
    public async Task Order_number_follows_the_ord_yyyymmdd_suffix_format()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var order = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var orderNumber = order.GetProperty("orderNumber").GetString()!;

        orderNumber.Should().StartWith("ORD-");
        orderNumber.Should().MatchRegex(@"^ORD-\d{8}-[0-9A-F]{6}$");
    }

    [Fact]
    public async Task Order_items_snapshot_the_price_at_checkout_time()
    {
        // Giá trong đơn phải là giá lúc checkout, không phải giá client gửi lên.
        var (variantId, price, sku) = await ApiTestHelper.GetFirstVariantAsync(_client);

        var order = await ApiTestHelper.CheckoutAsync(_client, variantId, quantity: 1);

        var item = order.GetProperty("items")[0];
        item.GetProperty("productVariantId").GetInt64().Should().Be(variantId);
        item.GetProperty("unitPrice").GetInt64().Should().Be(price);
        item.GetProperty("sku").GetString().Should().Be(sku);
        item.GetProperty("subtotal").GetInt64().Should().Be(price);
    }

    [Fact]
    public async Task Shipping_fee_is_zero_when_the_subtotal_reaches_the_free_shipping_threshold()
    {
        // FR-07: đơn từ 10.000.000 trở lên được miễn phí vận chuyển.
        var (variantId, price, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        // Chọn số lượng sao cho tổng >= ngưỡng miễn phí (tối đa 10 đơn vị/mỗi dòng).
        var quantity = (int)Math.Ceiling(10_000_000d / price);
        quantity = Math.Clamp(quantity, 1, 10);

        var order = await ApiTestHelper.CheckoutAsync(_client, variantId, quantity);

        var subtotal = order.GetProperty("subtotal").GetInt64();
        var shippingFee = order.GetProperty("shippingFee").GetInt64();
        var total = order.GetProperty("totalAmount").GetInt64();

        if (subtotal >= 10_000_000)
        {
            shippingFee.Should().Be(0);
        }
        else
        {
            shippingFee.Should().Be(30_000);
        }

        total.Should().Be(subtotal + shippingFee);
    }

    // ---------------------------------------------------------------------
    // Hủy đơn (FR-09)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Cancelling_an_order_records_the_reason_and_the_status_history()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);
        var order = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var orderId = order.GetProperty("id").GetInt64();

        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Khách đổi ý." });

        status.Should().Be(HttpStatusCode.OK);

        var cancelled = body.GetProperty("data");
        cancelled.GetProperty("status").GetString().Should().Be("CANCELLED");
        cancelled.GetProperty("cancelReason").GetString().Should().Be("Khách đổi ý.");
        cancelled.GetProperty("cancelledAt").ValueKind.Should().NotBe(JsonValueKind.Null);

        // Lịch sử trạng thái phải ghi lại cả lần tạo và lần hủy.
        var histories = cancelled.GetProperty("statusHistories").EnumerateArray().ToList();
        histories.Should().HaveCountGreaterThanOrEqualTo(2);
        histories[^1].GetProperty("fromStatus").GetString().Should().Be("PENDING");
        histories[^1].GetProperty("toStatus").GetString().Should().Be("CANCELLED");
    }

    [Fact]
    public async Task Cancelled_order_no_longer_appears_in_the_pending_filter()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);
        var order = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var orderId = order.GetProperty("id").GetInt64();

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Hủy để test lọc." });

        var pending = await ApiTestHelper.GetDataAsync(_client, "/api/v1/orders?status=PENDING");
        var pendingItems = pending.GetProperty("items").EnumerateArray().ToList();

        pendingItems.Should().NotContain(item => item.GetProperty("id").GetInt64() == orderId);
    }

    [Fact]
    public async Task Cancelled_order_appears_in_the_cancelled_filter()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);
        var order = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var orderId = order.GetProperty("id").GetInt64();

        await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            $"/api/v1/orders/{orderId}/cancel",
            new { reason = "Hủy để test lọc." });

        var cancelled = await ApiTestHelper.GetDataAsync(_client, "/api/v1/orders?status=CANCELLED");
        var items = cancelled.GetProperty("items").EnumerateArray().ToList();

        items.Should().Contain(item => item.GetProperty("id").GetInt64() == orderId);
    }

    // ---------------------------------------------------------------------
    // Danh mục (FR-04, FR-12)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Catalog_pagination_metadata_is_consistent_with_the_returned_items()
    {
        var (status, body) = await ApiTestHelper.SendAsync(_client, HttpMethod.Get, "/api/v1/products?page=1&pageSize=2");
        status.Should().Be(HttpStatusCode.OK);

        var data = body.GetProperty("data");
        var page = data.GetProperty("page").GetInt32();
        var pageSize = data.GetProperty("pageSize").GetInt32();
        var totalCount = data.GetProperty("totalCount").GetInt64();
        var items = data.GetProperty("items").EnumerateArray().ToList();

        page.Should().Be(1);
        pageSize.Should().Be(2);
        items.Count.Should().BeLessThanOrEqualTo(pageSize);
        totalCount.Should().BeGreaterThanOrEqualTo(items.Count);
    }

    [Fact]
    public async Task Catalog_total_pages_matches_total_count_and_page_size()
    {
        var data = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products?page=1&pageSize=1");

        var totalCount = data.GetProperty("totalCount").GetInt64();
        var totalPages = data.GetProperty("totalPages").GetInt32();
        var hasNextPage = data.GetProperty("hasNextPage").GetBoolean();

        totalPages.Should().Be((int)Math.Ceiling(totalCount / 1d));
        hasNextPage.Should().Be(totalPages > 1);
    }

    [Fact]
    public async Task Product_detail_returns_variants_with_positive_price_and_stock()
    {
        var products = await ApiTestHelper.GetDataAsync(_client, "/api/v1/products");
        var productId = products.GetProperty("items")[0].GetProperty("id").GetInt64();

        var detail = await ApiTestHelper.GetDataAsync(_client, $"/api/v1/products/{productId}");
        var variants = detail.GetProperty("variants").EnumerateArray().ToList();

        variants.Should().NotBeEmpty();
        foreach (var variant in variants)
        {
            variant.GetProperty("price").GetInt64().Should().BeGreaterThan(0);
            variant.GetProperty("stockQuantity").GetInt32().Should().BeGreaterThan(0);
            variant.GetProperty("sku").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task Categories_and_brands_endpoints_return_active_records()
    {
        var categories = await ApiTestHelper.GetDataAsync(_client, "/api/v1/categories");
        var brands = await ApiTestHelper.GetDataAsync(_client, "/api/v1/brands");

        categories.EnumerateArray().Should().NotBeEmpty();
        brands.EnumerateArray().Should().NotBeEmpty();

        foreach (var category in categories.EnumerateArray())
        {
            category.GetProperty("isActive").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task Creating_a_duplicate_category_returns_conflict()
    {
        var name = $"Danh mục regression {Guid.NewGuid():N}";

        var (createdStatus, createdBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/admin/categories",
            new { name });

        createdStatus.Should().Be(HttpStatusCode.Created);
        var categoryId = createdBody.GetProperty("data").GetProperty("id").GetInt64();

        var (duplicateStatus, duplicateBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/admin/categories",
            new { name });

        duplicateStatus.Should().Be(HttpStatusCode.Conflict);
        ApiTestHelper.AssertErrorEnvelope(duplicateBody, "CATEGORY_ALREADY_EXISTS");

        // Danh mục vừa tạo vẫn xuất hiện trong danh sách.
        var categories = await ApiTestHelper.GetDataAsync(_client, "/api/v1/categories");
        categories.EnumerateArray()
            .Should().Contain(category => category.GetProperty("id").GetInt64() == categoryId);
    }

    [Fact]
    public async Task Creating_a_category_with_blank_name_returns_validation_error()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/admin/categories",
            new { name = "   " });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Creating_a_duplicate_brand_returns_conflict()
    {
        var name = $"Thương hiệu regression {Guid.NewGuid():N}";

        var (createdStatus, _) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/admin/brands",
            new { name });

        createdStatus.Should().Be(HttpStatusCode.Created);

        var (duplicateStatus, duplicateBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/admin/brands",
            new { name });

        duplicateStatus.Should().Be(HttpStatusCode.Conflict);
        ApiTestHelper.AssertErrorEnvelope(duplicateBody, "BRAND_ALREADY_EXISTS");
    }

    // ---------------------------------------------------------------------
    // Tài khoản (FR-01, FR-02, FR-03)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Registered_customer_can_immediately_use_cart_and_checkout()
    {
        // Hồi quy end-to-end: đăng ký → lấy userId → giỏ hàng → checkout.
        var email = $"regression.{Guid.NewGuid():N}@example.com";

        var (registerStatus, registerBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Hồi Quy", phone = "0912345678", password = "Pass@123" });

        registerStatus.Should().Be(HttpStatusCode.Created);
        var userId = registerBody.GetProperty("data").GetProperty("userId").GetInt64();

        using var customerClient = ApiTestHelper.CreateUserClient(_factory, userId);

        var (variantId, price, _) = await ApiTestHelper.GetFirstVariantAsync(customerClient);

        var order = await ApiTestHelper.CheckoutAsync(customerClient, variantId, quantity: 1);
        order.GetProperty("customerId").GetInt64().Should().Be(userId);
        order.GetProperty("totalAmount").GetInt64().Should().Be(price);
    }

    [Fact]
    public async Task Customer_profile_reflects_the_latest_update()
    {
        var email = $"profile.{Guid.NewGuid():N}@example.com";

        var (_, registerBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Tên Cũ", phone = "0912345678", password = "Pass@123" });

        var userId = registerBody.GetProperty("data").GetProperty("userId").GetInt64();

        using var customerClient = ApiTestHelper.CreateUserClient(_factory, userId);

        var (updateStatus, _) = await ApiTestHelper.SendAsync(
            customerClient,
            HttpMethod.Patch,
            "/api/v1/me",
            new { fullName = "Tên Mới", phone = "0999888777" });

        updateStatus.Should().Be(HttpStatusCode.OK);

        var profile = await ApiTestHelper.GetDataAsync(customerClient, "/api/v1/me");
        profile.GetProperty("fullName").GetString().Should().Be("Tên Mới");
        profile.GetProperty("phone").GetString().Should().Be("0999888777");
        profile.GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Changing_password_to_the_same_value_is_rejected()
    {
        var email = $"password.{Guid.NewGuid():N}@example.com";

        var (_, registerBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Đổi Mật Khẩu", phone = "0912345678", password = "Pass@123" });

        var userId = registerBody.GetProperty("data").GetProperty("userId").GetInt64();

        using var customerClient = ApiTestHelper.CreateUserClient(_factory, userId);

        var (status, body) = await ApiTestHelper.SendAsync(
            customerClient,
            HttpMethod.Post,
            "/api/v1/me/change-password",
            new { currentPassword = "Pass@123", newPassword = "Pass@123" });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Changing_password_with_a_wrong_current_password_is_rejected()
    {
        var email = $"wrongpwd.{Guid.NewGuid():N}@example.com";

        var (_, registerBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Sai Mật", phone = "0912345678", password = "Pass@123" });

        var userId = registerBody.GetProperty("data").GetProperty("userId").GetInt64();

        using var customerClient = ApiTestHelper.CreateUserClient(_factory, userId);

        var (status, body) = await ApiTestHelper.SendAsync(
            customerClient,
            HttpMethod.Post,
            "/api/v1/me/change-password",
            new { currentPassword = "SaiMatKhau@999", newPassword = "MatKhauMoi@2026" });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Login_works_after_the_password_has_been_changed()
    {
        var email = $"relogin.{Guid.NewGuid():N}@example.com";

        var (_, registerBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Đăng Nhập Lại", phone = "0912345678", password = "Pass@123" });

        var userId = registerBody.GetProperty("data").GetProperty("userId").GetInt64();

        using var customerClient = ApiTestHelper.CreateUserClient(_factory, userId);

        var (changeStatus, _) = await ApiTestHelper.SendAsync(
            customerClient,
            HttpMethod.Post,
            "/api/v1/me/change-password",
            new { currentPassword = "Pass@123", newPassword = "MatKhauMoi@2026" });

        changeStatus.Should().Be(HttpStatusCode.OK);

        // Mật khẩu cũ không còn dùng được.
        var (oldStatus, _) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/login",
            new { email, password = "Pass@123" });
        oldStatus.Should().Be(HttpStatusCode.Unauthorized);

        // Mật khẩu mới đăng nhập được.
        var (newStatus, newBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/login",
            new { email, password = "MatKhauMoi@2026" });

        newStatus.Should().Be(HttpStatusCode.OK);
        newBody.GetProperty("data").GetProperty("userId").GetInt64().Should().Be(userId);
    }

    [Fact]
    public async Task Address_added_by_a_customer_is_visible_only_to_that_customer()
    {
        var email = $"address.{Guid.NewGuid():N}@example.com";

        var (_, registerBody) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/auth/register",
            new { email, fullName = "Khách Địa Chỉ", phone = "0912345678", password = "Pass@123" });

        var userId = registerBody.GetProperty("data").GetProperty("userId").GetInt64();

        using var customerClient = ApiTestHelper.CreateUserClient(_factory, userId);
        using var otherClient = ApiTestHelper.CreateUserClient(_factory, 999_003);

        var (addStatus, addBody) = await ApiTestHelper.SendAsync(
            customerClient,
            HttpMethod.Post,
            "/api/v1/me/addresses",
            new { receiverName = "Nguyễn Văn A", receiverPhone = "0912345678", addressText = "111 Đường A" });

        addStatus.Should().Be(HttpStatusCode.Created);
        var addressId = addBody.GetProperty("data").GetProperty("id").GetInt64();

        var ownAddresses = await ApiTestHelper.GetDataAsync(customerClient, "/api/v1/me/addresses");
        ownAddresses.EnumerateArray()
            .Should().Contain(address => address.GetProperty("id").GetInt64() == addressId);

        var otherAddresses = await ApiTestHelper.GetDataAsync(otherClient, "/api/v1/me/addresses");
        otherAddresses.EnumerateArray()
            .Should().NotContain(address => address.GetProperty("id").GetInt64() == addressId);
    }

    [Fact]
    public async Task Address_with_invalid_phone_returns_validation_error()
    {
        var (status, body) = await ApiTestHelper.SendAsync(
            _client,
            HttpMethod.Post,
            "/api/v1/me/addresses",
            new { receiverName = "Nguyễn Văn A", receiverPhone = "abc", addressText = "111 Đường A" });

        status.Should().Be(HttpStatusCode.BadRequest);
        ApiTestHelper.AssertErrorEnvelope(body, "VALIDATION_ERROR");
    }

    // ---------------------------------------------------------------------
    // Phân quyền (FR-06, FR-08, FR-09)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Customer_cannot_update_a_cart_item_belonging_to_another_customer()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            ApiTestHelper.Json);

        var cart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        var cartItemId = cart.GetProperty("items")[0].GetProperty("id").GetInt64();

        using var otherClient = ApiTestHelper.CreateUserClient(_factory, 999_004);

        var (status, body) = await ApiTestHelper.SendAsync(
            otherClient,
            HttpMethod.Put,
            $"/api/v1/cart/items/{cartItemId}",
            new { quantity = 5 });

        status.Should().Be(HttpStatusCode.NotFound);
        ApiTestHelper.AssertErrorEnvelope(body, "CART_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Customer_cannot_remove_a_cart_item_belonging_to_another_customer()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);

        await _client.PostAsJsonAsync(
            "/api/v1/cart/items",
            new { productVariantId = variantId, quantity = 1 },
            ApiTestHelper.Json);

        var cart = await ApiTestHelper.GetDataAsync(_client, "/api/v1/cart");
        var cartItemId = cart.GetProperty("items")[0].GetProperty("id").GetInt64();

        using var otherClient = ApiTestHelper.CreateUserClient(_factory, 999_005);

        var (status, body) = await ApiTestHelper.SendAsync(
            otherClient,
            HttpMethod.Delete,
            $"/api/v1/cart/items/{cartItemId}");

        status.Should().Be(HttpStatusCode.NotFound);
        ApiTestHelper.AssertErrorEnvelope(body, "CART_ITEM_NOT_FOUND");
    }

    [Fact]
    public async Task Orders_of_a_customer_are_not_visible_to_another_customer()
    {
        var (variantId, _, _) = await ApiTestHelper.GetFirstVariantAsync(_client);
        var order = await ApiTestHelper.CheckoutAsync(_client, variantId);
        var orderId = order.GetProperty("id").GetInt64();

        using var otherClient = ApiTestHelper.CreateUserClient(_factory, 999_006);

        var list = await ApiTestHelper.GetDataAsync(otherClient, "/api/v1/orders");
        list.GetProperty("items").EnumerateArray()
            .Should().NotContain(item => item.GetProperty("id").GetInt64() == orderId);
    }

    // ---------------------------------------------------------------------
    // Định dạng response thống nhất
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Every_error_response_uses_the_same_envelope_shape()
    {
        var cases = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Get, "/api/v1/products/999999", null),
            (HttpMethod.Get, "/api/v1/orders?status=NOT_A_STATUS", null),
            (HttpMethod.Post, "/api/v1/cart/items", new { productVariantId = 999999, quantity = 1 }),
            (HttpMethod.Post, "/api/v1/orders/checkout", new { receiverName = "", receiverPhone = "", shippingAddress = "" })
        };

        foreach (var (method, url, body) in cases)
        {
            var (status, payload) = await ApiTestHelper.SendAsync(_client, method, url, body);

            (status == HttpStatusCode.BadRequest || status == HttpStatusCode.NotFound).Should().BeTrue(
                "{0} {1} phải trả 4xx nhưng thực tế trả {2}.", method, url, status);

            payload.TryGetProperty("code", out _).Should().BeTrue(
                "{0} {1} phải có trường 'code'.", method, url);
            payload.TryGetProperty("message", out _).Should().BeTrue(
                "{0} {1} phải có trường 'message'.", method, url);
            payload.TryGetProperty("requestId", out _).Should().BeTrue(
                "{0} {1} phải có trường 'requestId'.", method, url);
            payload.TryGetProperty("timestamp", out _).Should().BeTrue(
                "{0} {1} phải có trường 'timestamp'.", method, url);
        }
    }

    [Fact]
    public async Task Successful_responses_use_the_same_envelope_shape()
    {
        var (status, payload) = await ApiTestHelper.SendAsync(_client, HttpMethod.Get, "/api/v1/products");

        status.Should().Be(HttpStatusCode.OK);

        payload.GetProperty("success").GetBoolean().Should().BeTrue();
        payload.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        payload.TryGetProperty("data", out _).Should().BeTrue();
        payload.TryGetProperty("timestamp", out _).Should().BeTrue();
    }
}
